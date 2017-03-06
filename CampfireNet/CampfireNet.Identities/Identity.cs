﻿using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CampfireNet.Utilities;

namespace CampfireNet.Identities {
   public class Identity {
      public readonly static byte[] BROADCAST_ID = new byte[CryptoUtil.HASH_SIZE];

      private RSAParameters privateKey;
      private byte[] identityHash;
      private IdentityManager identityManager;

      public Identity(IdentityManager identityManager, string name) : this(new RSACryptoServiceProvider(CryptoUtil.ASYM_KEY_SIZE_BITS), identityManager, name) { }

      public Identity(RSACryptoServiceProvider rsa, IdentityManager identityManager, string name) {
         privateKey = rsa.ExportParameters(true);
         identityHash = CryptoUtil.GetHash(privateKey.Modulus);

         this.identityManager = identityManager;

         TrustChain = null;
         PermissionsHeld = Permission.None;
         PermissionsGrantable = Permission.None;
         Name = name;
      }

      public Identity(IdentityManager identityManager, RSAParameters privateKey, string name) {
         this.privateKey = privateKey;
         identityHash = CryptoUtil.GetHash(privateKey.Modulus);

         this.identityManager = identityManager;

         TrustChain = null;
         PermissionsHeld = Permission.None;
         PermissionsGrantable = Permission.None;
         Name = name;
      }

      public TrustChainNode[] TrustChain { get; private set; }

      public Permission PermissionsHeld { get; private set; }
      public Permission PermissionsGrantable { get; private set; }

      public string Name { get; set; }

      public byte[] PublicIdentity => privateKey.Modulus;
      public byte[] PublicIdentityHash => identityHash;
      // TODO remove
      public RSAParameters PrivateKeyDebug => privateKey;
      public IdentityManager IdentityManager => identityManager;

      // gives this identity a trust chain to use
      public void AddTrustChain(byte[] trustChain) {
         TrustChainNode[] nodes = TrustChainUtil.SegmentChain(trustChain);
         bool isValid = TrustChainUtil.ValidateTrustChain(nodes);
         bool endsWithThis = nodes[nodes.Length - 1].ThisId.SequenceEqual(PublicIdentity);

         if (isValid && endsWithThis) {
            TrustChain = nodes;
            PermissionsHeld = nodes[nodes.Length - 1].HeldPermissions;
            PermissionsGrantable = nodes[nodes.Length - 1].GrantablePermissions;

            identityManager.AddIdentities(nodes);
         } else {
            throw new BadTrustChainException("Could not validate trust chain ending with this");
         }
      }

      // generates a new trust chain with this as the root node
      public void GenerateRootChain() {
         if (Name.Length > TrustChainUtil.UNASSIGNED_DATA_SIZE - 1) {
            throw new ArgumentException("Name too long");
         }

         var nameBytes = new byte[TrustChainUtil.UNASSIGNED_DATA_SIZE];
         using (var ms = new MemoryStream(nameBytes))
         using (var writer = new BinaryWriter(ms)) {
            writer.Write((byte)Name.Length);
            writer.Write(Encoding.UTF8.GetBytes(Name));
         }

         byte[] rootChain = TrustChainUtil.GenerateNewChain(new TrustChainNode[0], PublicIdentity, PublicIdentity, Permission.All,
                                       Permission.All, nameBytes, privateKey);
         PermissionsHeld = Permission.All;
         PermissionsGrantable = Permission.All;
         AddTrustChain(rootChain);
      }

      // generates a trust chain to pass to another client
      public byte[] GenerateNewChain(byte[] childId, Permission heldPermissions, Permission grantablePermissions,
                        string name) {
         bool canGrant = CanGrantPermissions(heldPermissions, grantablePermissions);

         if (canGrant) {
            if (name.Length > TrustChainUtil.UNASSIGNED_DATA_SIZE - 1) {
               throw new ArgumentException("Name too long");

            }
            byte[] nameBytes = new byte[TrustChainUtil.UNASSIGNED_DATA_SIZE];
            nameBytes[0] = (byte)name.Length;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(name), 0, nameBytes, 1, name.Length);

            return TrustChainUtil.GenerateNewChain(TrustChain, PublicIdentity, childId, heldPermissions,
                                   grantablePermissions, nameBytes, privateKey);
         } else {
            throw new InvalidPermissionException($"Insufficient authorization to grant permissions");
         }
      }

      // validates the given trust chain and adds the nodes to the list of known nodes, or returns false
      public bool ValidateAndAdd(byte[] trustChain) {
         TrustChainNode[] trustChainNodes = TrustChainUtil.SegmentChain(trustChain);
         return ValidateAndAdd(trustChainNodes);
      }

      public bool ValidateAndAdd(TrustChainNode[] trustChainNodes) {
         bool validChain = TrustChainUtil.ValidateTrustChain(trustChainNodes);
         if (!validChain) {
            return false;
         }

         bool sameRoot = TrustChain[0].ParentId.SequenceEqual(trustChainNodes[0].ParentId);
         if (!sameRoot) {
            return false;
         }

         for (int i = 0; i < trustChainNodes.Length; i++) {
            identityManager.AddIdentity(trustChainNodes[i], Name);
         }

         return true;
      }

      // () asymmetric encrypt
      // <[sender hash][recipient hash]([sender hash][message])[signature]>
      //  [32         ][32            ] [32         ][msg len] [256      ]
      public BroadcastMessageDto EncodePacket(byte[] message, byte[] remoteModulus = null) {
         if (remoteModulus != null && remoteModulus.Length != CryptoUtil.ASYM_KEY_SIZE_BYTES) {
            throw new CryptographicException("Bad key size");
         }

         byte[] senderHash = CryptoUtil.GetHash(privateKey.Modulus);
         byte[] recipientHash;
         byte[] processedMessage;

         if (remoteModulus == null) {
            recipientHash = BROADCAST_ID;
            processedMessage = message;
         } else {
            recipientHash = CryptoUtil.GetHash(remoteModulus);
            byte[] senderAndMessage = new byte[CryptoUtil.HASH_SIZE + message.Length];
            Buffer.BlockCopy(senderHash, 0, senderAndMessage, 0, CryptoUtil.HASH_SIZE);
            Buffer.BlockCopy(message, 0, senderAndMessage, CryptoUtil.HASH_SIZE, message.Length);
            processedMessage = CryptoUtil.AsymmetricEncrypt(senderAndMessage, remoteModulus);
         }

         byte[] payload = new byte[2 * CryptoUtil.HASH_SIZE + processedMessage.Length];
         Buffer.BlockCopy(senderHash, 0, payload, 0, CryptoUtil.HASH_SIZE);
         Buffer.BlockCopy(recipientHash, 0, payload, CryptoUtil.HASH_SIZE, CryptoUtil.HASH_SIZE);
         Buffer.BlockCopy(processedMessage, 0, payload, 2 * CryptoUtil.HASH_SIZE, processedMessage.Length);

         byte[] signature = CryptoUtil.Sign(payload, privateKey);

         return new BroadcastMessageDto {
            SourceIdHash = senderHash,
            DestinationIdHash = recipientHash,
            Payload = processedMessage,
            Signature = signature
         };
      }

      public bool TryDecodePayload(BroadcastMessageDto broadcastMessage, out byte[] decryptedPayload) {
         var senderHash = broadcastMessage.SourceIdHash;
         var receiverHash = broadcastMessage.DestinationIdHash;
         var payload = broadcastMessage.Payload;
         var signature = broadcastMessage.Signature;

         bool unicast = receiverHash.SequenceEqual(identityHash);
         bool broadcast = receiverHash.SequenceEqual(BROADCAST_ID);

         if (!unicast && !broadcast) {
            decryptedPayload = null;
            return false;
         }

         byte[] modulus;
         TrustChainNode senderNode = identityManager.LookupIdentity(senderHash);
         if (senderNode == null) {
            throw new InvalidStateException("Sender hash not recognized");
         } else {
            modulus = senderNode.ThisId;
         }

         if (!CryptoUtil.Verify(broadcastMessage, modulus, signature)) {
            throw new CryptographicException("Could not verify message");
         }

         if (broadcast) {
            decryptedPayload = payload;
            return true;
         }

         byte[] decryptedSenderAndMessage = broadcast ? payload : CryptoUtil.AsymmetricDecrypt(payload, privateKey);
         byte[] decryptedSenderHash = new byte[CryptoUtil.HASH_SIZE];

         Buffer.BlockCopy(decryptedSenderAndMessage, 0, decryptedSenderHash, 0, CryptoUtil.HASH_SIZE);

         if (!decryptedSenderHash.SequenceEqual(senderHash)) {
            throw new CryptographicException("Sender hash does not equal sender hash in message");
         }

         var messageSize = decryptedSenderAndMessage.Length - CryptoUtil.HASH_SIZE;
         decryptedPayload = new byte[messageSize];
         Buffer.BlockCopy(decryptedSenderAndMessage, CryptoUtil.HASH_SIZE, decryptedPayload, 0, messageSize);
         return true;
      }

      public void SaveKey(string path) {
         byte[] data = CryptoUtil.SerializeKey(privateKey);
         File.WriteAllBytes(path, data);
      }

      // whether the given permissions can be granted by this node
      public bool CanGrantPermissions(Permission heldPermissions, Permission grantablePermissions) {
         return PermissionsHeld.HasFlag(Permission.Invite) &&
              TrustChainUtil.ValidatePermissions(PermissionsGrantable, heldPermissions) &&
              TrustChainUtil.ValidatePermissions(heldPermissions, grantablePermissions);
      }
   }
}
