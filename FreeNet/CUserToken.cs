﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace FreeNet
{
	public class CUserToken
	{
		public Socket socket					{ get; set; }
		public SocketAsyncEventArgs receiveArgs { get; private set; }
		public SocketAsyncEventArgs sendArgs	{ get; private set; }

		// 바이트를 패킷 형식으로 해석해주는 해석기.
		CMessageResolver messageResolve;

		// session객체. 어플리케이션 딴에서 구현하여 사용.
		IPeer peer;

		// 전송할 패킷을 보관해놓는 큐. 1-Send로 처리하기 위한 큐이다.
		Queue<CPacket> sendingQueue;
		// sending_queue lock처리에 사용되는 객체.
		private object lockQueue;

		public CUserToken()
		{
			//Console.WriteLine(this + " CUserToken");
			this.lockQueue		= new object();

			this.messageResolve	= new CMessageResolver();
			this.peer			= null;
			this.sendingQueue	= new Queue<CPacket>();
		}

		public void SetPeer(IPeer _peer)
		{
			Console.WriteLine(this + " SetPeer _peer:{0}", _peer);
			this.peer = _peer;
		}

		public void SetEventArgs(SocketAsyncEventArgs _argsReceive, SocketAsyncEventArgs _argsSend)
		{
			Console.WriteLine(this + " SetEventArgs(CUserToken에 연결해두기)\r\n _argsReceive:{0}\r\n _argsSend:{1}", _argsReceive, _argsSend);
			this.receiveArgs	= _argsReceive;
			this.sendArgs		= _argsSend;
		}

		/// <summary>
		///	이 매소드에서 직접 바이트 데이터를 해석해도 되지만 Message resolver클래스를 따로 둔 이유는
		///	추후에 확장성을 고려하여 다른 resolver를 구현할 때 CUserToken클래스의 코드 수정을 최소화 하기 위함이다.
		/// </summary>
		/// <param name="_buffer"></param>
		/// <param name="_offset"></param>
		/// <param name="_transfered"></param>
		public void ReceiveRead(byte[] _buffer, int _offset, int _transfered)
		{
			Console.WriteLine(this + " ReceiveRead\r\n _buffer:{0} _offset:{1} _transfered:{2}", _buffer, _offset, _transfered);
			this.messageResolve.ReadReceive(
				_buffer, _offset, _transfered, 
				OnMessage
				);
		}

		//_callback(new Const<byte[]>(this.messageBuffer));
		void OnMessage(Const<byte[]> _buffer)
		{
			Console.WriteLine(this + " OnMessage _buffer:{0}", _buffer);
			if (this.peer != null)
			{
				this.peer.ParseCode(_buffer);
			}
		}

		public void OnRemoved()
		{
			Console.WriteLine(this + " OnRemoved clear Queue");
			this.sendingQueue.Clear();

			if (this.peer != null)
			{
				this.peer.OnRemoved();
			}
		}

		/// <summary>
		/// 패킷을 전송한다.
		/// 큐가 비어 있을 경우에는 큐에 추가한 뒤 바로 SendAsync매소드를 호출하고,
		/// 데이터가 들어있을 경우에는 새로 추가만 한다.
		/// 
		/// 큐잉된 패킷의 전송 시점 :
		///		현재 진행중인 SendAsync가 완료되었을 때 큐를 검사하여 나머지 패킷을 전송한다.
		/// </summary>
		/// <param name="_packetOrg"></param>
		public void SendCode(CPacket _packetOrg)
		{			
			Console.WriteLine(this + " Send _packetOrg:{0}", _packetOrg );
			//Console.WriteLine(" #### > 복사해서 큐에 넣어준다. ㅠㅠ");
			//CPacket _packetCopy = new CPacket();
			//_packetOrg.copy_to(_packetCopy);
			CPacket _packetCopy = _packetOrg;

			lock (this.lockQueue)
			{
				if (this.sendingQueue.Count > 0)
				{
					// 큐에 무언가가 들어 있다면 아직 이전 전송이 완료되지 않은 상태이므로 큐에 추가만 하고 리턴한다.
					// 현재 수행중인 SendAsync가 완료된 이후에 큐를 검사하여 데이터가 있으면 SendAsync를 호출하여 전송해줄 것이다.
					Console.WriteLine("Queue is not empty. Copy and Enqueue a msg. protocol id : " + _packetOrg.code);
					this.sendingQueue.Enqueue(_packetCopy);
				}
				else
				{
					// 큐가 비어 있다면 큐에 추가하고 바로 비동기 전송 매소드를 호출한다.
					this.sendingQueue.Enqueue(_packetCopy);
					StartSend();
				}
			}
		}

		/// <summary>
		/// 비동기 전송을 시작한다.
		/// </summary>
		void StartSend()
		{
			Console.WriteLine(this + " StartSend");
			lock (this.lockQueue)
			{
				// 전송이 아직 완료된 상태가 아니므로 데이터만 가져오고 큐에서 제거하진 않는다.
				CPacket _packet = this.sendingQueue.Peek();

				// 헤더에 패킷 사이즈를 기록한다.
				_packet.WriteSize();

				// 이번에 보낼 패킷 사이즈 만큼 버퍼 크기를 설정하고
				this.sendArgs.SetBuffer(this.sendArgs.Offset, _packet.position);
				// 패킷 내용을 SocketAsyncEventArgs버퍼에 복사한다.
				Array.Copy(_packet.buffer, 0, this.sendArgs.Buffer, this.sendArgs.Offset, _packet.position);
				Console.WriteLine(" > sendArgs.Offset:{0} ~> .position:{1} ", sendArgs.Offset, _packet.position);

				// 비동기 전송 시작.
				bool pending = this.socket.SendAsync(this.sendArgs);
				Console.WriteLine(" > 전송등록 pending:{0}", pending);
				if (!pending)
				{
					Console.WriteLine("  > 전송등록후 바로 보내버렸다.");
					SendProcess(this.sendArgs);
				}
			}
		}

		static int sent_count = 0;
		static object cs_count = new object();
		/// <summary>
		/// 비동기 전송 완료시 호출되는 콜백 매소드.
		/// </summary>
		/// <param name="_argsSend"></param>
		public void SendProcess(SocketAsyncEventArgs _argsSend)
		{
			Console.WriteLine(this + " SendProcess _argsSend:" + _argsSend);
			if (_argsSend.BytesTransferred <= 0 || _argsSend.SocketError != SocketError.Success)
			{
				Console.WriteLine("Failed to send. error {0}, transferred {1}", _argsSend.SocketError, _argsSend.BytesTransferred);
				return;
			}

			lock (this.lockQueue)
			{
				// count가 0이하일 경우는 없겠지만...
				if (this.sendingQueue.Count <= 0)
				{
					throw new Exception("Sending queue count is less than zero!");
				}

				//todo:재전송 로직 다시 검토~~ 테스트 안해봤음.
				// 패킷 하나를 다 못보낸 경우는??
				int _size = this.sendingQueue.Peek().position;
				if (_argsSend.BytesTransferred != _size)
				{
					string _error = string.Format("Need to send more! transferred {0},  packet size {1}", _argsSend.BytesTransferred, _size);
					Console.WriteLine(_error);
					return;
				}


				//System.Threading.Interlocked.Increment(ref sent_count);
				//Debug영역...
				lock (cs_count)
				{
					++sent_count;
					//if (sent_count % 20000 == 0)
					{
						Console.WriteLine(" > process send : {0}, transferred {1}, sent count {2}",
							_argsSend.SocketError, _argsSend.BytesTransferred, sent_count);
					}
				}

				// 전송 완료된 패킷을 큐에서 제거한다.
				//CPacket packet = this.sending_queue.Dequeue();
				//CPacket.destroy(packet);
				CPacket _packet = this.sendingQueue.Dequeue();
				CPacketBufferManager.Push(_packet);
				Console.WriteLine(" > 사용한 CPacket 다시 넣어줌.. 수량:" + CPacketBufferManager.GetCount());

				// 아직 전송하지 않은 대기중인 패킷이 있다면 다시한번 전송을 요청한다.
				if (this.sendingQueue.Count > 0)
				{
					StartSend();
				}
			}
		}

		//void send_directly(CPacket msg)
		//{
		//	msg.record_size();
		//	this.send_event_args.SetBuffer(this.send_event_args.Offset, msg.position);
		//	Array.Copy(msg.buffer, 0, this.send_event_args.Buffer, this.send_event_args.Offset, msg.position);
		//	bool pending = this.socket.SendAsync(this.send_event_args);
		//	if (!pending)
		//	{
		//		process_send(this.send_event_args);
		//	}
		//}

		public void Disconnect()
		{
			Console.WriteLine(this + " Disconnect");
			// close the socket associated with the client
			try
			{
				this.socket.Shutdown(SocketShutdown.Send);
			}
			// throws if client process has already closed
			catch (Exception _e) {
				Console.WriteLine("error:{0}", _e);
			}
			this.socket.Close();
		}

		public void StartKeepAlive()
		{
			Console.WriteLine(this + " StartKeepAlive");
			System.Threading.Timer keepalive = new System.Threading.Timer((object e) =>
			{
				CPacket _packet = CPacket.Create(0);
				_packet.WriteInt(0);
				SendCode(_packet);
			}, null, 0, 3000);
		}
	}
}
