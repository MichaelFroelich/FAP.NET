/*
				GNU GENERAL PUBLIC LICENSE
		                   Version 3, 29 June 2007

	 Copyright (C) 2007 Free Software Foundation, Inc. <http://fsf.org/>
	 Everyone is permitted to copy and distribute verbatim copies
	 of this license document, but changing it is not allowed.
	 
	 Author: Michael J. Froelich
 */

using System;
using FAP;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace FAP //Functional active pages , Functional programming And Pages, Free API Production, FAP seems like a good name!
{
	public class Server
	{
		/* Constants */
		const int SERVERWARM = 1500;
		//Have a maximum of this many connections possible
		const int SERVERCOOL = 251;
		//If the connection count falls below this level, quickly make a lot more
		const string HTTP = "HTTP/1.0 ";
		//HTTP initial header
		const string BREAKER = "\r\n";
		//New line breaker
		const string VERSION = "0.1";
		//Current version
		const int TIMEOUT = 5;
		//Send and receive timeout
		const bool NODELAY = true;
		//Nagle algorithm thing
		/* State booleans */
		//bool queueProcessLock = false;
		//Locks and unlocks the queue
		bool needsreset = true;
		//Used to reset the listeners every 5 minutes (remember, Apache and NGINX do this too!)
		/* Other private variables */
		int listenercount = 0;
		//Used to count how many listeners are active
		TcpListener listener;
		//the main listener, will be duplicated in later versions, perhaps for added speed
		//Queue<TcpClient> incomming;
		//The queue
		SortedList<string, Page> pagelist;
		//Where all the pages are kept

		//List<IAsyncResult> listenerlist; //This has... no way of actually working

		bool ever = true;

		public bool IsRunning {
			get { return ever; }
		}

		int port;

		public int Port {
			get { return port; }
			set {
				port = value;
				ResetListener(true);
			}
		}
		//Port to listen on
		/// <summary>
		/// Address to listen on
		/// </summary>
		/// <value>The address.</value>
		IPAddress address;

		public IPAddress Address {
			get { return address; }
			set {
				address = value;
				ResetListener(true);
			}
		}

		/// <summary>
		/// Changes the value of the query character, ie localhost/yourpage? where the query character here is '?'. Default is '?'.
		/// </summary>
		/// <value>The query character.</value>
		public char QueryCharacter {
			get;
			set;
		} = '?';

		/// <summary>
		/// Adds a page, mostly
		/// </summary>
		/// <param name="p">P.</param>
		public void AddPage(Page p)
		{
			if (p != null)
				pagelist.Add(p.Path, p);
		}

		/// <summary>
		/// Stops the server
		/// </summary>
		public void Stop()
		{
			ever = false;
			listener.Stop();
		}
		//TODO: Handle the generics better, something that doesn't require collection.Values, because I can't be assured all collections have that function
		/// <summary>
		/// Initializes a new instance of the <see cref="FAP.Server"/> class.
		/// </summary>
		/// <param name="inList">A list of pages, try "Your data structure".Values.</param>
		/// <param name="IPAddy">IP address, likely the default: 127.0.0.1 (please use a static webserver in conjunction with FAP.net)</param>
		/// <param name="port">Port for a complete socket, default is 1024</param>
		public Server(IEnumerable<Page> inList = null, string IPAddy = "127.0.0.1", int port = 1024)
		{
			pagelist = new SortedList<string, Page>();
			this.address = IPAddress.Parse(IPAddy);
			this.port = port;
			//listenerlist = new List<IAsyncResult>()
			listener = new TcpListener(Address, Port);
			//incomming = new Queue<TcpClient>(SERVERWARM * 2);
			listener.Server.NoDelay = NODELAY;
			listener.Start();
			if (inList != null)
				foreach (Page p in inList)
					if (p != null)
						pagelist.Add(p.Path, p);
			Task.Factory.StartNew(ListenerLoop); //Staart listening
		}

		/// <summary>
		/// Used for timing, error recovery and CPU safety
		/// </summary>
		void ListenerLoop()
		{ //Yielding is purely a replacement for nice. Nice is a bad idea, but yielding every other method, not so much
			for (; ever;) {
				Thread.Yield();							// <.<
				Thread.Sleep(1); 						// First I look left, then I sleep, then I look right
				Thread.Yield(); 						// >.>
				if (needsreset)							//Check if we need to reset the 
					ResetListener();					//If not, reset all the listeners
				Thread.Yield();
				if (listenercount < SERVERWARM)
					Task.Factory.StartNew(Listen);		//As of writing, starting a task is more performant than a new thread
			}
			Console.Error.WriteLine("Server ended at " + System.DateTime.UtcNow);
		}

		~Server()
		{
			Stop();
		}

		async void Listen()
		{
			try {
				listenercount++;
				if (listenercount > SERVERCOOL) {		//If there's less than SC listeners, it's a good idea to create more
					do {
						Thread.Yield(); 				//Yielding informs the system to do something else
						await Task.Delay(50); 			//This basically means "check back in 50ms," it doesn't actually delay/suspend anything
					} while (listener.Pending());		//Do not spawn whilst the server is being spammed, reserve the ticks for processing
				}
				listener.BeginAcceptTcpClient(ListenerCallback, listener); //second param may be listener
			} catch (Exception e) {
				Console.Error.WriteLine(DateTime.UtcNow + " Listener error: " + e.Message);
				if (!listener.Server.IsBound)
					ResetListener(true);
			}
		}

		void ListenerCallback(IAsyncResult result)
		{
			try {
				//Task.Factory.StartNew(() => Parse(listener.EndAcceptTcpClient(result)));
				var c = listener.EndAcceptTcpClient(result);
				Task.Factory.StartNew(() => Parse(c));	//Probably cargo cult, but whatever 
				listenercount--; 						//Do this as soon as posible in case more listeners need to be spawned
				//c.NoDelay = NODELAY; 					//Increases speed automagically somewhat
				//Task.Factory.StartNew(() => Parse(c));	//As of Mon, 20 Jul 2015 22:51, I'm assuming this has its own queue
				//incomming.Enqueue(c);					//Load onto the process queue, note, use a queue (FIFO) and not a stack (LIFO)
				//if (!queueProcessLock)
				//	ProcessQueue();
			} catch (Exception e) {
				Console.Error.WriteLine(DateTime.UtcNow + " Callback error, probably just resetting but specifically: " + e.Message);
			}
		}
		/*
		void ProcessQueue()
		{
			queueProcessLock = true;					//A VERY cheap way to ensure the queue isn't being processed multiple times
			while (incomming.Count > 0) {
				var t = incomming.Dequeue();
				Task.Factory.StartNew(() => Parse(t));				//Provided this is only being called in one place, there is no issue
			}
			queueProcessLock = false;
		}
*/
		void Parse(TcpClient client)
		{
			char input;
			//char input2;
			char method1;
			char method2;
			string code = "404 Not Found"; //Default
			//string s = "";
			string message;
			string output = "";
			string querystring = "";
			string path = "";
			StringBuilder builder = new StringBuilder();
			client.NoDelay = NODELAY;
			if (client == null || !client.Connected)
				return; 
			try {
				//using (var reader = new StreamReader(stream))//using (var writer = new StreamWriter(stream)) 
				using (var stream = client.GetStream()) {//In the end, the reward for only using one stream is immeasurable				
					//if (/*stream.DataAvailable && */stream.CanRead && stream.CanWrite) {
					if (stream.CanRead) {
						//s = builder.Append((char)stream.ReadByte() + (char)stream.ReadByte()).ToString(); //G
						//s = string.Format("" + (char)stream.ReadByte() + (char)stream.ReadByte()); //G
						method1 = (char)stream.ReadByte(); //E
						method2 = (char)stream.ReadByte(); //T , get it?
						do {
							input = (char)stream.ReadByte();
							if (input == ' ') {
								stream.ReadByte(); //Trims '/'
								break;
							}
						} while (input != '\n'/*!char.IsControl(input) && input != '\uffff'*/); //Always be safe
						do {
							input = (char)stream.ReadByte();
							if (input == QueryCharacter) {
								path = builder.ToString();
								builder.Clear();
							} else if (input == ' ')
								break;
							else {
								builder.Append(input);
							}
						} while (input != '\n'/*!char.IsControl(input) && input != '\uffff'*/);//input != '\n' && input != '\r' && input != '\uffff' && input != '\0');
						querystring = builder.ToString();
						builder.Clear();
						/*
						var splitstring = s.Split(' ');
						if (splitstring.Length >= 2) {
							splitstring = splitstring[1].TrimStart('/').Split(QueryCharacter);
							if (splitstring.Length >= 2) {			//Bottleneck source C# wizards please advise
								path = splitstring[0];
								querystring = splitstring[1];
							}
						}
						*/
						//*
						while (stream.DataAvailable) { //Gotta be safe... would like to have used this first time' later
							input = (char)stream.ReadByte();
							if (input == '\n') { 				//This is an inappropriate means of doing this
								input = (char)stream.ReadByte();
								if (input == '\r') {			//Basicaly, if it reads a null then a carriage, it breaks
									break;
								}
							}
						}
						//*/
						/*
						input2 = (char)stream.ReadByte(); //This is actually a slower, although more expressive, means
						while (input != '\n' && input2 != '\r') {
							input = input2;
							input2 = (char)stream.ReadByte();
						}
						//*/

						while (stream.DataAvailable) { 			//Necessary, as not all clients send content-length
							builder.Append((char)stream.ReadByte());
						}
						message = builder.ToString();
						builder.Clear();
						Page thispage;
						if (!string.IsNullOrEmpty(path) && pagelist.TryGetValue(path, out thispage)) {
							switch (method1) {
								case 'g':
								case 'G':
									output = thispage.Get(querystring, message);
									if (output != null) //as in, actually null
										code = "200 Ok";
									break;
								case 'p':
								case 'P':
									{
										if (method2 == 'U') {
											output = thispage.Put(querystring, message);
											if (output != null) //as in, actually null
												code = "200 Ok";
										} else if (method2 == 'O') {
											output = thispage.Post(querystring, message);
											if (output != null) //as in, actually null
												code = "200 Ok";
										}
										break;
									}
								case 'd':
								case 'D':
									{
										output = thispage.Delete(querystring, message);
										if (output != null) //as in, actually null
											code = "200 Ok";
										break;
									}
								default:
									code = "444";
									break;
							}
						}

						/*
						builder.Append(HTTP).AppendLine(code); //Besides when impossible, explicitly adding "\n" is better than "appendline"
						builder.Append("Server: FAP.NET ").Append(VERSION).Append(" Codename: M\r\n");
						builder.Append("Date: ").Append(System.DateTime.UtcNow.ToString("R")).Append("\r\n");
						builder.Append("Pragma: no-cache\r\n");
						builder.Append("Connection: close\r\n");
						builder.Append("Content-type: application/json; charset=us-ascii\r\n");
						builder.Append("Cache-control: no-cache\r\n");
						builder.Append("Content-Length: ").Append(output.Length).Append("\r\n");
						builder.Append("\r\n");
						*/

						//I'm told that many append functions is only performant in loops
						builder.Append(HTTP + code + "\r\n" +
						"Server: FAP.NET " + VERSION + " Codename: Meisscanne\r\n" +
						"Date: " + DateTime.UtcNow.ToString("R") + "\r\n" +
						"Pragma: no-cache\r\n" +
						"Connection: close\r\n" +
						"Content-type: application/json; charset=us-ascii\r\n" +
						"Cache-control: no-cache\r\n" +
						"Content-Length: " + (output == null ? 0 : output.Length) + "\r\n\r\n"
						);
						//if (stream.CanWrite) //Apparently this does more than check a variable, so it's best to call this as late as possible
						switch (code[0]) {
							case '2':
								builder.AppendLine(output + "\n" + BREAKER);
								//builder.AppendLine(BREAKER);
								stream.Write(Encoding.ASCII.GetBytes(builder.ToString()), 0, builder.Length);
								break;
							case '3':
								builder.AppendLine(BREAKER);
								stream.Write(Encoding.ASCII.GetBytes(builder.ToString()), 0, builder.Length);
								break;
							case '4':
								builder.AppendLine(BREAKER);
								stream.Write(Encoding.ASCII.GetBytes(builder.ToString()), 0, builder.Length);
								break;
							default:
								const string defres = "404 Not Found" + BREAKER + BREAKER; // this should be both hidden and performantly const
								stream.Write(Encoding.ASCII.GetBytes(defres), 0, defres.Length);
								break;
						}
					}
					stream.Flush(); //Flush
					stream.Close(); //Close
					client.Close(); //Close
				}

			} catch (Exception e) { 
				Console.Error.WriteLine(DateTime.UtcNow + " That was a bad client \n\t\t" + client.ToString() + "\n" + e.ToString());   
				ResetListener(true);
			}
		}

		async void ResetListener(bool force = false)
		{// Whilst questionable, many servers kill old listeners and reset them after about 5 minutes
			if (!force) { //I believe this is the easiest/most efficient means of timing.
				needsreset = false;
				await Task.Delay(300000);
			}
			try {
				listener.Server.Close();	//Fixes an "address in use" error
				listener.Stop();
				//incomming.Clear();
				listenercount = 0;
				listener = new TcpListener(Address, Port);
				listener.Server.NoDelay = NODELAY;
				listener.Start();
				needsreset |= !force;
				while (listenercount < SERVERCOOL) {
					listenercount++;
					listener.BeginAcceptTcpClient(ListenerCallback, listener);
				}

			} catch (Exception e) {
				Console.Error.WriteLine(System.DateTime.UtcNow + " Reset Error: " + e.Message);
			}
		}


	}
}
