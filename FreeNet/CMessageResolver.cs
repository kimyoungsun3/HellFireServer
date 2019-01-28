using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FreeNet
{
   	class Defines
	{
		public static readonly short HEADERSIZE = 2;
	}

	/// <summary>
	/// [header][body] 구조를 갖는 데이터를 파싱하는 클래스.
	/// - header : 데이터 사이즈. Defines.HEADERSIZE에 정의된 타입만큼의 크기를 갖는다.
	///				2바이트일 경우 Int16, 4바이트는 Int32로 처리하면 된다.
	///				본문의 크기가 Int16.Max값을 넘지 않는다면 2바이트로 처리하는것이 좋을것 같다.
	/// - body : 메시지 본문.
	/// </summary>
	class CMessageResolver
	{
		public delegate void CompletedMessageCallback(Const<byte[]> buffer);

		// 메시지 사이즈.
		int messageSize;

		// 진행중인 버퍼.
		byte[] messageBuffer = new byte[1024];

		// 현재 진행중인 버퍼의 인덱스를 가리키는 변수.
		// 패킷 하나를 완성한 뒤에는 0으로 초기화 시켜줘야 한다.
		int messageOffset;

		// 읽어와야 할 목표 위치.
		int messageToRead;

		// 남은 사이즈.
		int remainBytes;

		public CMessageResolver()
		{
			this.messageSize = 0;
			this.messageOffset = 0;
			this.messageToRead = 0;
			this.remainBytes = 0;
		}

		/// <summary>
		/// 소켓 버퍼로부터 데이터를 수신할 때 마다 호출된다.
		/// 데이터가 남아 있을 때 까지 계속 패킷을 만들어 callback을 호출 해 준다.
		/// 하나의 패킷을 완성하지 못했다면 버퍼에 보관해 놓은 뒤 다음 수신을 기다린다.
		/// </summary>
		/// <param name="_buffer"></param>
		/// <param name="_offset"></param>
		/// <param name="_transferred"></param>
		public void ReadReceive(byte[] _buffer, int _offset, int _transferred, CompletedMessageCallback _callback)
		{
			Console.WriteLine(this + " ReadReceive\r\n _buffer:{0} _offset:{1} _transferred:{2}\r\n _callback(받은메세지를 이콜백으로 처리함)", _buffer, _offset, _transferred);
			// 이번 receive로 읽어오게 될 바이트 수.
			this.remainBytes = _transferred;

			// 원본 버퍼의 포지션값.
			// 패킷이 여러개 뭉쳐 올 경우 원본 버퍼의 포지션은 계속 앞으로 가야 하는데 그 처리를 위한 변수이다.
			int _offset2 = _offset;

			// 남은 데이터가 있다면 계속 반복한다.
			while (this.remainBytes > 0)
			{
				Console.WriteLine(" > _offset:{0} _offset2:{1} _transferred:{2}\r\n"
					+ " > messageOffset:{3} remainBytes:{4} messageToRead:{5} messageSize:{6}", 
					_offset, _offset2, _transferred, 
					messageOffset, remainBytes, messageToRead, messageSize);
				bool _completed = false;

				// 헤더만큼 못읽은 경우 헤더를 먼저 읽는다.
				if (this.messageOffset < Defines.HEADERSIZE)
				{
					// 목표 지점 설정(헤더 위치까지 도달하도록 설정).
					this.messageToRead = Defines.HEADERSIZE;
					Console.WriteLine("   > (헤더읽기) messageOffset:{0} messageToRead:{1}", messageOffset, messageToRead);

					_completed = ReadUtil(_buffer, ref _offset2, _offset, _transferred);
					if (!_completed)
					{
						// 아직 다 못읽었으므로 다음 receive를 기다린다.
						Console.WriteLine("    > (**** 헤더아직 안들어와서 리턴 *****");
						return;
					}

					// 헤더 하나를 온전히 읽어왔으므로 메시지 사이즈를 구한다.
					this.messageSize = GetBodySize();
					Console.WriteLine("   > 해더만읽고 messageSize:{0}", messageSize);

					// 다음 목표 지점(헤더 + 메시지 사이즈).
					this.messageToRead = Defines.HEADERSIZE + this.messageSize;
					Console.WriteLine("   > messageSize:{0} messageToRead:{1}", messageSize, messageToRead);
				}

				// 메시지를 읽는다.
				_completed = ReadUtil(_buffer, ref _offset2, _offset, _transferred);

				if (_completed)
				{
					Console.WriteLine("   > 메세지를 정상읽음 > 메세지 처리하러가기~~~");

					// 패킷 하나를 완성 했다.
					_callback(new Const<byte[]>(this.messageBuffer));

					ClearBuffer();
				}
				else
				{
					Console.WriteLine("   > 메세지를 비정상 ****> 자동대기중~~~");
				}
			}
		}

		/// <summary>
		/// 목표지점으로 설정된 위치까지의 바이트를 원본 버퍼로부터 복사한다.
		/// 데이터가 모자랄 경우 현재 남은 바이트 까지만 복사한다.
		/// </summary>
		/// <param name="_buffer"></param>
		/// <param name="_offset"></param>
		/// <param name="_transferred"></param>
		/// <param name="size_to_read"></param>
		/// <returns>다 읽었으면 true, 데이터가 모자라서 못 읽었으면 false를 리턴한다.</returns>
		bool ReadUtil(byte[] _buffer, ref int _offset2, int _offset, int _transferred)
		{
			Console.WriteLine(this + " ReadUtil\r\n _buffer:{0}, ref _offset2:{1} _offset:{2} _transferred:{3}", _buffer, _offset2, _offset, _transferred);
			//if (this.current_position >= _offset + _transferred)
			if (_offset2 >= _offset + _transferred)
			{
				// 들어온 데이터 만큼 다 읽은 상태이므로 더이상 읽을 데이터가 없다.
				Console.WriteLine("  > 들어온 데이타 사이즈만큼 다읽음");
				return false;
			}

			// 읽어와야 할 바이트.
			// 데이터가 분리되어 올 경우 이전에 읽어놓은 값을 빼줘서 부족한 만큼 읽어올 수 있도록 계산해 준다.
			int _copySize = this.messageToRead - this.messageOffset;
			Console.WriteLine("  > _copySize:{0} = messageToRead:{1} - messageOffset:{2}", _copySize, messageToRead, messageOffset);

			// 앗! 남은 데이터가 더 적다면 가능한 만큼만 복사한다.
			if (this.remainBytes < _copySize)
			{
				//해더에 1byte만 올경우.
				Console.WriteLine("   > 급변경(전) _copySize:{0} remainBytes{1}", _copySize, remainBytes);
				_copySize = this.remainBytes;
				Console.WriteLine("   > 급변경(후) _copySize:{0} remainBytes{1}", _copySize, remainBytes);
			}

			// 버퍼에 복사.
			Array.Copy(_buffer, _offset2, this.messageBuffer, this.messageOffset, _copySize);

			_offset2			+= _copySize;   // 원본 버퍼 포지션 이동.
			this.messageOffset	+= _copySize;   // 타겟 버퍼 포지션도 이동.
			this.remainBytes	-= _copySize;   // 남은 바이트 수.
			Console.WriteLine("  > 복사후 포지션정리 _offset2:{0}  messageOffset:{1} remainBytes:{2}", _offset2, messageOffset, remainBytes);

			// 목표지점에 도달 못했으면 false
			if (this.messageOffset < this.messageToRead)
			{
				//해더에 1byte만 올경우.
				Console.WriteLine("   > 지정된곳까지 남음");
				return false;
			}
			else
			{
				Console.WriteLine("   > 지정된곳까지 다읽음");
				return true;
			}
		}


		private int GetBodySize()
		{
			Console.WriteLine(this + " GetBodySize");
			// 헤더 타입의 바이트만큼을 읽어와 메시지 사이즈를 리턴한다.

			Type _type = Defines.HEADERSIZE.GetType();
			if (_type.Equals(typeof(Int16)))
			{
				return BitConverter.ToInt16(this.messageBuffer, 0);
			}
			return BitConverter.ToInt32(this.messageBuffer, 0);
		}

		void ClearBuffer()
		{
			Console.WriteLine(this + " ClearBuffer");
			//Array.Clear(this.messageBuffer, 0, this.messageBuffer.Length);

			this.messageOffset = 0;
			this.messageSize = 0;

		}
	}
}
