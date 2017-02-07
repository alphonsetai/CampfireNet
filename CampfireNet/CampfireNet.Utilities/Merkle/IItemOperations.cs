namespace CampfireNet.Utilities.Merkle {
   public interface IItemOperations<T> {
      byte[] Serialize(T item);
   }
}