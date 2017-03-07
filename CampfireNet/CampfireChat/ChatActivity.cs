﻿
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using CampfireNet.Identities;
using CampfireNet.Utilities;
using static CampfireNet.Utilities.Channels.ChannelsExtensions;

namespace CampfireChat {
   [Activity(Label = "Chat", ParentActivity = typeof(MainActivity))]
	public class ChatActivity : Activity
	{
		private RecyclerView chatRecyclerView;
		private ChatAdapter chatAdapter;
		private RecyclerView.LayoutManager chatLayoutManager;

//		private List<MessageEntry> testMessages;
	   private ChatRoomContext chatRoomContext;

	   private ChatRoomViewModel viewModel;

	   protected override void OnCreate(Bundle savedInstanceState)
		{
//         testMessages = new List<MessageEntry> {
//            new MessageEntry("Name 1", "This is a test message 1"),
//            new MessageEntry("Name 2", "This is a test message 2"),
//            new MessageEntry("Name 3", "This is a test message 3"),
//            new MessageEntry("Name 2", "This is a test message 4 really long message here one that is sure to overflow. How about some more text here and see if we can get it to three lines - or even more! How far can we go?"),
//            new MessageEntry("Name 3", "This is a test message 5"),
//            new MessageEntry("Name 1", "These are yet more messages designed to be long and take up space."),
//            new MessageEntry("Name 2", "These are yet more messages designed to be long and take up space."),
//            new MessageEntry("Name 3", "These are yet more messages designed to be long and take up space."),
//            new MessageEntry("Name 1", "These are yet more messages designed to be long and take up space."),
//            new MessageEntry("Name 2", "These are yet more messages designed to be long and take up space.")
//         };

         base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.Chat);

			var toolbar = FindViewById<Android.Widget.Toolbar>(Resource.Id.Toolbar);
			SetActionBar(toolbar);
			ActionBar.SetDisplayHomeAsUpEnabled(true);

			chatRecyclerView = (RecyclerView)FindViewById(Resource.Id.Messages);
			chatRecyclerView.HasFixedSize = true;

			chatLayoutManager = new LinearLayoutManager(this);
			chatRecyclerView.SetLayoutManager(chatLayoutManager);

			chatAdapter = new ChatAdapter();
			chatAdapter.ItemClick += OnItemClick;
			chatRecyclerView.SetAdapter(chatAdapter);

			Title = Intent.GetStringExtra("title") ?? "Chat";

		   var chatId = Intent.GetByteArrayExtra("chatId");
		   chatRoomContext = Globals.CampfireChatClient.ChatRoomTable.GetOrCreate(IdentityHash.GetFlyweight(chatId));
		   viewModel = chatRoomContext.CreateViewModelAndSubscribe((sender, e) => {
		      var message = e.Message;
		      if (message.ContentType != ChatMessageContentType.Text)
		         throw new NotImplementedException();

		      chatAdapter.AddEntry(new MessageEntry(message.FriendlySenderName, Encoding.UTF8.GetString(message.ContentRaw)));
		   });

		   var sendButton = FindViewById<Button>(Resource.Id.SendMessage);
		   sendButton.Click += HandleSendButtonClicked;
		}

	   private void OnItemClick(object sender, byte[] id)
		{
			Toast.MakeText(this, $"got id {Encoding.UTF8.GetString(id)}", ToastLength.Short).Show();

			//Intent intent = new Intent(this, typeof(ChatActivity));
			//intent.PutExtra("id", id);
			//StartActivity(intent);
		}

	   private void HandleSendButtonClicked(object sender, EventArgs e) {
	      var sendTextbox = FindViewById<EditText>(Resource.Id.Input);
	      var text = sendTextbox.Text;
         Console.WriteLine(text);
	      new Thread(() => {
            Console.WriteLine("!");
	         viewModel.SendMessageText(text);
         }).Start();
	      sendTextbox.Text = "";
      }

	   public override bool OnCreateOptionsMenu(IMenu menu)
		{
			MenuInflater.Inflate(Resource.Menu.chat_menu, menu);
			return base.OnCreateOptionsMenu(menu);
	   }

	   public override bool OnOptionsItemSelected(IMenuItem item) {
	      if (item.ItemId == Android.Resource.Id.Home) {
	         Finish();
	      }

	      return base.OnOptionsItemSelected(item);
	   }
   }
}
