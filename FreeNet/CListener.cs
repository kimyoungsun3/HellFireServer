using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FreeNet
{
	class CListener
	{
        // 비동기 Accept를 위한 EventArgs.
		SocketAsyncEventArgs acceptArgs;

		Socket acceptSocket;

        // Accept처리의 순서를 제어하기 위한 이벤트 변수.
		AutoResetEvent autoResetEvent;

        // 새로운 클라이언트가 접속했을 때 호출되는 콜백.
		public delegate void NewclientHandler(Socket _socket, object _token);
		public NewclientHandler onAcceptNewClient;

        public CListener()
		{
			Console.WriteLine(this + " Construtor");
			this.onAcceptNewClient = null;
        }

		public void Start(string _host, int _port, int _backlog)
		{
			Console.WriteLine(this + " Start host:{0} port:{1} backlog:{2}", _host, _port, _backlog);
			this.acceptSocket = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			IPAddress _address;
			if (_host == "0.0.0.0")
			{
				_address = IPAddress.Any;
			}
			else
			{
				_address = IPAddress.Parse(_host);
			}
			IPEndPoint _ipEndPoint = new IPEndPoint(_address, _port);

			try
			{
				acceptSocket.Bind(_ipEndPoint);
				acceptSocket.Listen(_backlog);

				this.acceptArgs				= new SocketAsyncEventArgs();
				this.acceptArgs.Completed	+= new EventHandler<SocketAsyncEventArgs>(OnAcceptCallback);
				

				Thread _thread = new Thread(AcceptTcpClient);
				_thread.Start();
			}
			catch (Exception _e)
			{
				Console.WriteLine(this + " error:" + _e.Message);
			}
		}

		/// <summary>
		/// 루프를 돌며 클라이언트를 받아들입니다.
		/// 하나의 접속 처리가 완료된 후 다음 accept를 수행하기 위해서 event객체를 통해 흐름을 제어하도록 구현되어 있습니다.
		/// </summary>AcceptTcpClient
		void AcceptTcpClient()
		{
			Console.WriteLine(this + " AcceptTcpClient(Thread 동작중....)");
			this.autoResetEvent = new AutoResetEvent(false);

			while (true)
			{
				Console.WriteLine(" > AcceptTcpClient > 접속대기등록(acceptSocket.AcceptAsync(acceptArgs))");
				// SocketAsyncEventArgs를 재사용 하기 위해서 null로 만들어 준다.
				this.acceptArgs.AcceptSocket = null;

				bool _pending = true;
				try
				{
					// 비동기 accept를 호출하여 클라이언트의 접속을 받아들입니다.
					// 비동기 매소드 이지만 동기적으로 수행이 완료될 경우도 있으니
					// 리턴값을 확인하여 분기시켜야 합니다.
					_pending = acceptSocket.AcceptAsync(this.acceptArgs);

					//대기중에 다시 대기를 걸면 오류가 발생한다...
					//_pending = acceptSocket.AcceptAsync(this.acceptArgs);
				}
				catch (Exception _e)
				{
					Console.WriteLine(_e.Message);
					continue;
				}

				// 즉시 완료 되면 이벤트가 발생하지 않으므로 리턴값이 false일 경우 콜백 매소드를 직접 호출해 줍니다.
				// pending상태라면 비동기 요청이 들어간 상태이므로 콜백 매소드를 기다리면 됩니다.
				// http://msdn.microsoft.com/ko-kr/library/system.net.sockets.socket.acceptasync%28v=vs.110%29.aspx
				if (!_pending)
				{
					OnAcceptCallback(null, this.acceptArgs);
				}

				Console.WriteLine(" > AcceptTcpClient > Wait...");
				// 클라이언트 접속 처리가 완료되면 이벤트 객체의 신호를 전달받아 다시 루프를 수행하도록 합니다.
				this.autoResetEvent.WaitOne();
				Console.WriteLine(" > AcceptTcpClient > Wake up");

				// *팁 : 반드시 WaitOne -> Set 순서로 호출 되야 하는 것은 아닙니다.
				//      Accept작업이 굉장히 빨리 끝나서 Set -> WaitOne 순서로 호출된다고 하더라도 
				//      다음 Accept 호출 까지 문제 없이 이루어 집니다.
				//      WaitOne매소드가 호출될 때 이벤트 객체가 이미 signalled 상태라면 스레드를 대기 하지 않고 계속 진행하기 때문입니다.
			}
		}

        /// <summary>
        /// AcceptAsync의 콜백 매소드
        /// </summary>
        /// <param name="_sender"></param>
        /// <param name="_acceptArgs">AcceptAsync 매소드 호출시 사용된 EventArgs</param>
		void OnAcceptCallback(object _sender, SocketAsyncEventArgs _acceptArgs)
		{
			Console.WriteLine(this + " OnAcceptAsync (신규유저접속시도)\r\n -> _sender:{0}\r\n -> _acceptArgs:{1}", _sender, _acceptArgs);
			if (_acceptArgs.SocketError == SocketError.Success)
            {
				Console.WriteLine("  -> NewClient Success");
				// 접속에 따른 OS가 받아온 Socket를 SocketAsynEvnetArgs에 실어서 보내줌.
				// 새로 생긴 소켓을 보관해 놓은뒤~
                Socket _socket		= _acceptArgs.AcceptSocket;
				CUserToken _token	= _acceptArgs.UserToken as CUserToken;

                // 다음 연결을 받아들인다.
                this.autoResetEvent.Set();

                // 이 클래스에서는 accept까지의 역할만 수행하고 클라이언트의 접속 이후의 처리는
                // 외부로 넘기기 위해서 콜백 매소드를 호출해 주도록 합니다.
                // 이유는 소켓 처리부와 컨텐츠 구현부를 분리하기 위함입니다.
                // 컨텐츠 구현부분은 자주 바뀔 가능성이 있지만, 소켓 Accept부분은 상대적으로 변경이 적은 부분이기 때문에
                // 양쪽을 분리시켜주는것이 좋습니다.
                // 또한 클래스 설계 방침에 따라 Listen에 관련된 코드만 존재하도록 하기 위한 이유도 있습니다.
                if (this.onAcceptNewClient != null)
                {
                    this.onAcceptNewClient(_socket, _token);
                }

				return;
            }
            else
			{
				Console.WriteLine("  -> NewClient Fail");
				//todo:Accept 실패 처리.
				//Console.WriteLine("Failed to accept client.");
			}

			// 다음 연결을 받아들인다.
            this.autoResetEvent.Set();
		}
	}
}
