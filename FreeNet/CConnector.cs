using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace FreeNet
{
	/// <summary>
	/// Endpoint정보를 받아서 서버에 접속한다.
	/// 접속하려는 서버 하나당 인스턴스 한개씩 생성하여 사용하면 된다.
	/// </summary>
	public class CConnector
	{
		public delegate void VOID_FUN_TOKEN(CUserToken token);
		public VOID_FUN_TOKEN onConnected { get; set; }

		// 원격지 서버와의 연결을 위한 소켓.
		Socket client;
		CNetworkService networkService;

		public CConnector(CNetworkService _service)
		{
			networkService	= _service;
			onConnected		= null;
		}

		//public void connect(IPEndPoint remote_endpoint)
		//{
		//	this.client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);		
		//	// 비동기 접속을 위한 event args.
		//	SocketAsyncEventArgs event_arg = new SocketAsyncEventArgs();
		//	event_arg.Completed			+= OnConnectAsync;
		//	event_arg.RemoteEndPoint	= remote_endpoint;
		//	bool pending = this.client.ConnectAsync(event_arg);
		//	if (!pending)
		//	{
		//		OnConnectAsync(null, event_arg);
		//	}
		//}

		public void Connect(IPEndPoint _ipEndPoint, AddressFamily _addressFamily = AddressFamily.InterNetwork)
		{
			client = new Socket(_addressFamily, SocketType.Stream, ProtocolType.Tcp);

			// 비동기 접속을 위한 event args.
			SocketAsyncEventArgs _connectArgs	= new SocketAsyncEventArgs();
			_connectArgs.Completed				+= OnConnectAsync;
			_connectArgs.RemoteEndPoint			= _ipEndPoint;
			bool _pending = client.ConnectAsync(_connectArgs);
			if (!_pending)
			{
				OnConnectAsync(null, _connectArgs);
			}
		}

		void OnConnectAsync(object _sender, SocketAsyncEventArgs _connectArgs)
		{
			Console.WriteLine(this + " OnConnectAsync");
			if (_connectArgs.SocketError == SocketError.Success)
			{
				Console.WriteLine(" > Socket Connect Success > Connect completd!");
				CUserToken _token = new CUserToken();

				// 데이터 수신 준비.
				networkService.OnConnectCompleted(client, _token);
				if (onConnected != null)
				{
					onConnected(_token);
				}
			}
			else
			{
				// failed.
				Console.WriteLine(string.Format("Failed to connect. {0}", _connectArgs.SocketError));
			}
		}
	}
}
