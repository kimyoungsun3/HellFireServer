using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace FreeNet
{
    public class CNetworkService
    {
		int connectedCount;
		CListener clientListener;
		SocketAsyncEventArgsPool receiveArgsPool;
		SocketAsyncEventArgsPool sendArgsPool;
		BufferManager bufferManager;

		public delegate void SessionHandler(CUserToken token);
		public SessionHandler onSessionCreated { get; set; }

		// configs.
		int maxConnections;
		int bufferSize;
		readonly int pre_alloc_count = 2;		// read, write

		public CNetworkService()
		{
			Console.WriteLine(this + " Construtor");
			this.connectedCount = 0;
			this.onSessionCreated = null;
		}

		// Initializes the server by preallocating reusable buffers and 
		// context objects.  These objects do not need to be preallocated 
		// or reused, but it is done this way to illustrate how the API can 
		// easily be used to create reusable objects to increase server performance.
		//
		public void Initialize()
		{
			Console.WriteLine(this + " Initialize");
			this.maxConnections = 10000;
			this.bufferSize = 1024;

			this.bufferManager			= new BufferManager(this.maxConnections * this.bufferSize * this.pre_alloc_count, this.bufferSize);
			this.receiveArgsPool	= new SocketAsyncEventArgsPool(this.maxConnections);
			this.sendArgsPool		= new SocketAsyncEventArgsPool(this.maxConnections);

			// Allocates one large byte buffer which all I/O operations use a piece of.  This gaurds 
			// against memory fragmentation
			this.bufferManager.InitBuffer();

			// preallocate pool of SocketAsyncEventArgs objects
			SocketAsyncEventArgs _arg;

			for (int i = 0; i < this.maxConnections; i++)
			{
				// 동일한 소켓에 대고 send, receive를 하므로
				// user token은 세션별로 하나씩만 만들어 놓고 
				// receive, send EventArgs에서 동일한 token을 참조하도록 구성한다.
				CUserToken _token = new CUserToken();

				// receive pool
				{
					//Pre-allocate a set of reusable SocketAsyncEventArgs
					_arg			= new SocketAsyncEventArgs();
					_arg.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceiveAsync);
					_arg.UserToken	= _token;

					// assign a byte buffer from the buffer pool to the SocketAsyncEventArg object
					this.bufferManager.SetBuffer(_arg);

					// add SocketAsyncEventArg to the pool
					this.receiveArgsPool.Push(_arg);
				}

				// send pool
				{
					//Pre-allocate a set of reusable SocketAsyncEventArgs
					_arg			= new SocketAsyncEventArgs();
					_arg.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendAsync);
					_arg.UserToken	= _token;

					// assign a byte buffer from the buffer pool to the SocketAsyncEventArg object
					this.bufferManager.SetBuffer(_arg);

					// add SocketAsyncEventArg to the pool
					this.sendArgsPool.Push(_arg);
				}
			}
		}

		public void Listen(string _host, int _port, int _backlog)
		{
			Console.WriteLine(this + " Listen({0}, {1}, {2})", _host, _port, _backlog);
			this.clientListener = new CListener();
			this.clientListener.onAcceptNewClient += OnAcceptProcess;
			this.clientListener.Start(_host, _port, _backlog);
		}

		/// <summary>
		/// 새로운 클라이언트가 접속 성공 했을 때 호출됩니다.
		/// AcceptAsync의 콜백 매소드에서 호출되며 여러 스레드에서 동시에 호출될 수 있기 때문에 공유자원에 접근할 때는 주의해야 합니다.
		/// </summary>
		/// <param name="_newClientSocket"></param>
		void OnAcceptProcess(Socket _newClientSocket, object _token)
		{
			Console.WriteLine(this + " OnAcceptProcess(신규유저가 접속됨(콜백받음))\r\n > _newClientSocket:{0}\r\n > token:{1}", _newClientSocket, _token);
			//todo:
			// peer list처리.

			Interlocked.Increment(ref this.connectedCount);
			//Interlocked 클래스는 int 형 값을 증가시키거나 감소시키는데 사용한다. 
			//멀티 쓰레드 환경에서 하나의 int 형 전역 변수를 공유한다고 생각해보자. 
			//이런 경우에 A 쓰레드와 B 쓰레드가 값을 동시에 읽어와서 
			//B 쓰레드가 수정한 값을 저장하고, 
			//A 쓰레드가 다시 수정한 값을 저장하게 되면 
			//B 쓰레드의 변경사항을 잃어버리게 된다. 

			Console.WriteLine("  > connectedCount:{0} ThreadID:{1} _newClientSocket.Handle:{2}",
				this.connectedCount, 
				Thread.CurrentThread.ManagedThreadId, 
				_newClientSocket.Handle);

			// 플에서 하나 꺼내와 사용한다.
			SocketAsyncEventArgs _argsReceive	= this.receiveArgsPool.Pop();
			SocketAsyncEventArgs _argsSend		= this.sendArgsPool.Pop();
			CUserToken _userToken = null;
			Console.WriteLine("  > argsReceive, argsSend -> 스택에서 뺴서");

			if (this.onSessionCreated != null)
			{
				Console.WriteLine("   > 서버세션(UserToken) 리스트에 등록");
				_userToken = _argsReceive.UserToken as CUserToken;
				this.onSessionCreated(_userToken);
			}

			Console.WriteLine("  > 연결 : UserTokent(_newClientSocket, _argsReceive, _argsSend)");
			ReceiveWaitBegin(_newClientSocket, _argsReceive, _argsSend);
			//user_token.start_keepalive();
		}

		/// <summary>
		/// todo:검토중...
		/// 원격 서버에 접속 성공 했을 때 호출됩니다.
		/// </summary>
		/// <param name="_socket"></param>
		public void OnConnectCompleted(Socket _socket, CUserToken _token)
		{
			Console.WriteLine(this + " >>>>>>>>>(호출되면 보자) OnConnectCompleted _socket:{0} _token:{1}", _socket, _token);
			// SocketAsyncEventArgsPool에서 빼오지 않고 그때 그때 할당해서 사용한다.
			// 풀은 서버에서 클라이언트와의 통신용으로만 쓰려고 만든것이기 때문이다.
			// 클라이언트 입장에서 서버와 통신을 할 때는 접속한 서버당 두개의 EventArgs만 있으면 되기 때문에 그냥 new해서 쓴다.
			// 서버간 연결에서도 마찬가지이다.
			// 풀링처리를 하려면 c->s로 가는 별도의 풀을 만들어서 써야 한다.
			SocketAsyncEventArgs _receiveArgs = new SocketAsyncEventArgs();
			_receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceiveAsync);
			_receiveArgs.UserToken = _token;
			_receiveArgs.SetBuffer(new byte[1024], 0, 1024);

			SocketAsyncEventArgs _sendArg = new SocketAsyncEventArgs();
			_sendArg.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendAsync);
			_sendArg.UserToken = _token;
			_sendArg.SetBuffer(new byte[1024], 0, 1024);

			ReceiveWaitBegin(_socket, _receiveArgs, _sendArg);
		}


		void ReceiveWaitBegin(Socket _socket, SocketAsyncEventArgs _argsReceive, SocketAsyncEventArgs _argsSend)
		{
			Console.WriteLine(this + " *** ReceiveWaitBegin (각유저 패킷받기 시작) ***\r\n _socket:{0}\r\n _argsReceive:{1}\r\n _argsSend:{2}", _socket, _argsReceive, _argsSend);

			// receive_args, send_args 아무곳에서나 꺼내와도 된다. 
			// 둘다 동일한 CUserToken을 물고 있다.
			CUserToken _token = _argsReceive.UserToken as CUserToken;
			_token.SetEventArgs(_argsReceive, _argsSend);
			// 생성된 클라이언트 소켓을 보관해 놓고 통신할 때 사용한다.
			_token.socket = _socket;

			Console.WriteLine(" > 방금접속유저 받기비동기등록 > _socket.ReceiveAsync(_argsReceive)");
			bool _pending = _socket.ReceiveAsync(_argsReceive);
			if (!_pending)
			{
				ReceiveProcess(_argsReceive);
			}
		}

		// This method is called whenever a receive or send operation is completed on a socket 
		//
		// <param name="e">SocketAsyncEventArg associated with the completed receive operation</param>
		void OnReceiveAsync(object _sender, SocketAsyncEventArgs _argsReceive)
		{
			Console.WriteLine(this + " OnReceiveAsync(유저패킷 비동기 콜백들어옴) \r\n _sender:{0},\r\n _e{1}", _sender, _argsReceive);
			if (_argsReceive.LastOperation == SocketAsyncOperation.Receive)
			{
				ReceiveProcess(_argsReceive);
				return;
			}

			throw new ArgumentException("The last operation completed on the socket was not a receive.");
		}
		// This method is invoked when an asynchronous receive operation completes. 
		// If the remote host closed the connection, then the socket is closed.  
		//
		private void ReceiveProcess(SocketAsyncEventArgs _receiveArgs)
		{
			Console.WriteLine(this + " ReceiveProcess(받음처리)\r\n _receiveArgs:{0}", _receiveArgs);
			// check if the remote host closed the connection
			CUserToken _token = _receiveArgs.UserToken as CUserToken;
			if (_receiveArgs.BytesTransferred > 0 && _receiveArgs.SocketError == SocketError.Success)
			{
				Console.WriteLine(" > ***** 메세지받음(처리시작) *****");
				_token.ReceiveRead(_receiveArgs.Buffer, _receiveArgs.Offset, _receiveArgs.BytesTransferred);

				Console.WriteLine(" > ***** 메세지받기(처리완료) *****\r\n > 소켓에 메세지 받기 비동기 재등록)");
				bool _pending = _token.socket.ReceiveAsync(_receiveArgs);
				if (!_pending)
				{
					// Oh! stack overflow??
					ReceiveProcess(_receiveArgs);
				}
			}
			else
			{
				Console.WriteLine(" > 메세지받음(종료)\r\n error {0},  transferred {1}", _receiveArgs.SocketError, _receiveArgs.BytesTransferred);
				CloseClientSocket(_token);
			}
		}



		// This method is called whenever a receive or send operation is completed on a socket 
		//
		// <param name="e">SocketAsyncEventArg associated with the completed send operation</param>
		void OnSendAsync(object _sender, SocketAsyncEventArgs _argsSend)
		{
			Console.WriteLine(this + " OnSendAsync(유저에게 보냄)\r\n _sender:{0}\r\n _argsSend:{1}", _sender, _argsSend);
			CUserToken _token = _argsSend.UserToken as CUserToken;
			_token.SendProcess(_argsSend);
		}


		public void CloseClientSocket(CUserToken _token)
		{
			Console.WriteLine(this + " **** CloseClientSocket(접속끊어짐 > argsReceive, argsSend 돌려주기) ****\r\n _token:{0}", _token);
			_token.OnRemoved();

			// Free the SocketAsyncEventArg so they can be reused by another client
			// 버퍼는 반환할 필요가 없다. SocketAsyncEventArg가 버퍼를 물고 있기 때문에
			// 이것을 재사용 할 때 물고 있는 버퍼를 그대로 사용하면 되기 때문이다.
			if (this.receiveArgsPool != null)
			{
				this.receiveArgsPool.Push(_token.receiveArgs);
			}

			if (this.sendArgsPool != null)
			{
				this.sendArgsPool.Push(_token.sendArgs);
			}
		}
    }
}
