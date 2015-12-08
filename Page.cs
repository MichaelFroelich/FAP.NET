﻿/*
				GNU GENERAL PUBLIC LICENSE
		                   Version 3, 29 June 2007

	 Copyright (C) 2007 Free Software Foundation, Inc. <http://fsf.org/>
	 Everyone is permitted to copy and distribute verbatim copies
	 of this license document, but changing it is not allowed.
	 
	 	Author: Michael J. Froelich
 */

using System;
using FAP;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Net.Sockets;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Reflection;

namespace FAP //Change this name!
{
	/// <summary>
	/// The "Page" class, can be treated as an actual page if you simply return strings from the get method.
	/// </summary>
	public class Page
	{
		/// <summary>
		/// Gets the initial path of your api URL, ie localhost/api? where "api" is the path name. You do not need to include either a forward slash or a question mark, simply the path name
		/// </summary>
		/// <value>as a string</value>
		public string Path{ protected set; get; }

		/// <summary>
		/// When "got"; are the headers sent from the client machine to the server with the user's HTTP version as the first line (mostly separated by \r\n). When "set"; are extra headers from the server to the client, generally ended in \r\n for each new line.
		/// Do not modify the headers if you do not wish to add additional headers, just leave it as is. Hint; use Split('\n') and use a case/switch on the first character of each resultant string
		/// </summary>
		/// <value>The headers.</value>
		public string Headers{ get; set; }

		/// <summary>
		/// The user's IP address as a string, setting this value does nothing.
		/// </summary>
		/// <value>The user's IP address</value>
		public string UserIP { get; set; }

		/// <summary>
		/// Gets the UserAgent string, very useful for distinguishing two users of the same IP, setting this value does nothing.
		/// </summary>
		/// <value>The user's "user agent"</value>
		public string UserAgent { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="FAP.Page"/> class.
		/// </summary>
		public Page() //Simple for now
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FAP.Page"/> class. Construct by passing through a string that represents the path to this page, ie localhost/api? where "api" is the path name. You do not need to include either a forward slash or a question mark, simply the path name
		/// </summary>
		/// <param name="path">Path to this page as a string, without a terminator or forward slash</param>
		public Page(string path) //Simple for now
		{
			Path = path;
		}


		/// <summary>
		/// Set a function which will be called when accessing this page through a "get" HTTP method.
		/// </summary>
		/// <value>The get function</value>
		public Func<string, string, string> get{ get; set; }

		/// <summary>
		/// Set a function which will be called when accessing this page through a "put" HTTP method.
		/// </summary>
		/// <value>The put function</value>
		public Func<string, string, string> put{ get; set; }

		/// <summary>
		/// Set a function which will be called when accessing this page through a "post" HTTP method.
		/// </summary>
		/// <value>The post function</value>
		public Func<string, string, string> post{ get; set; }

		/// <summary>
		/// Set a function which will be called when accessing this page through a "delete" HTTP method.
		/// </summary>
		/// <value>The delete function</value>
		public Func<string, string, string> delete{ get; set; }

		/// <summary>
		/// Override this for "object oriented" behaviour of defining the get function for this page.
		/// </summary>
		/// <param name="queryString">Other commands used in the url string, ie /api?command1.command2.other. It's recommended you terminate with a '.' symbol</param>
		/// <param name="messageContent">IP address as a pain text string, then a new line ('\n'), then the message body content found after the carriage return after the HTTP headers</param>
		public virtual string Get(string queryString, string messageContent)
		{
			if (this.get != null) {
				return this.get(queryString, messageContent);
			}
			return null;
		}

		/// <summary>
		/// Override this for "object oriented" behaviour of defining the put function for this page.
		/// </summary>
		/// <param name="queryString">Other commands used in the url string, ie /api?command1.command2.other. It's recommended you terminate with a '.' symbol</param>
		/// <param name="messageContent">IP address as a pain text string, then a new line ('\n'), then the message body content found after the carriage return after the HTTP headers</param>
		public virtual string Put(string queryString, string messageContent)
		{
			if (put != null) {
				return this.put(queryString, messageContent);
			}
			return null;
		}

		/// <summary>
		/// Override this for "object oriented" behaviour of defining the post function for this page.
		/// </summary>
		/// <param name="queryString">Other commands used in the url string, ie /api?command1.command2.other. It's recommended you terminate with a '.' symbol</param>
		/// <param name="messageContent">IP address as a pain text string, then a new line ('\n'), then the message body content found after the carriage return after the HTTP headers</param>
		public virtual string Post(string queryString, string messageContent)
		{
			if (this.post != null) {
				return this.post(queryString, messageContent);
			}
			return null;
		}

		/// <summary>
		/// Override this for "object oriented" behaviour of defining the delete function for this page.
		/// </summary>
		/// <param name="queryString">Other commands used in the url string, ie /api?command1.command2.other. It's recommended you terminate with a '.' symbol</param>
		/// <param name="messageContent">IP address as a pain text string, then a new line ('\n'), then the message body content found after the carriage return after the HTTP headers</param>
		public virtual string Delete(string queryString, string messageContent)
		{
			if (this.delete != null) {
				return this.delete(queryString, messageContent);
			}
			return null;
		}

	}
}
