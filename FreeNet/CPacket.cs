using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FreeNet
{
	/// <summary>
	/// byte[] 버퍼를 참조로 보관하여 pop_xxx 매소드 호출 순서대로 데이터 변환을 수행한다.
	/// </summary>
	public class CPacket
	{
		public IPeer owner		{ get; private set; }
		public byte[] buffer	{ get; private set; }
		public int position		{ get; private set; }
		public Int16 code		{ get; private set; }
		public CPacket()
		{
			this.buffer = new byte[1024];
		}

		//--------------------------------------------
		//
		//--------------------------------------------
		public static CPacket Create(Int16 _code)
		{
			//CPacket packet = new CPacket();
			CPacket _packet = CPacketBufferManager.Pop();
			_packet.WriteCode(_code);
			Console.WriteLine(" > 풀링된 CPacket 남은수량:{1}", _code, CPacketBufferManager.GetCount());
			return _packet;
		}

		public static void Destroy(CPacket _packet)
		{
			CPacketBufferManager.Push(_packet);
		}

		public CPacket(byte[] _buffer, IPeer _owner)
		{
			// 참조로만 보관하여 작업한다.
			// 복사가 필요하면 별도로 구현해야 한다.
			this.buffer		= _buffer;			
			this.position	= Defines.HEADERSIZE; // 헤더는 읽을필요 없으니 그 이후부터 시작한다.
			this.owner		= _owner;
		}

		

		public Int16 pop_protocol_id()
		{
			return ReadShort();
		}

		public void copy_to(CPacket target)
		{
			target.WriteCode(this.code);
			target.overwrite(this.buffer, this.position);
		}

		public void overwrite(byte[] source, int position)
		{
			Array.Copy(source, this.buffer, source.Length);
			this.position = position;
		}

        public void PosEnd()
        {
            this.position += 1;
        }

		//-----------------------------------------
		//
		//-----------------------------------------
		public byte ReadByte()
		{
			byte _data		= (byte)BitConverter.ToInt16(this.buffer, this.position);
			this.position	+= sizeof(byte);
			return _data;
		}

		public Int16 ReadShort()
		{
			Int16 _data		= BitConverter.ToInt16(this.buffer, this.position);
			this.position	+= sizeof(Int16);
			return _data;
		}

		public Int32 ReadInt()
		{
			Int32 _data		= BitConverter.ToInt32(this.buffer, this.position);
			this.position	+= sizeof(Int32);
			return _data;
		}

		//Single - float
        public Single ReadFloat()
        {
            Single _str		= BitConverter.ToSingle(this.buffer, this.position);
            this.position	+= sizeof(Single);
            return _str;
        }

		public string ReadString()
		{
			// 문자열 길이는 최대 2바이트 까지. 0 ~ 32767
			Int16 _len		= BitConverter.ToInt16(this.buffer, this.position);
			this.position	+= sizeof(Int16);

			// 인코딩은 utf8로 통일한다.
			string _str		= System.Text.Encoding.UTF8.GetString(this.buffer, this.position, _len);
			this.position	+= _len;

			return _str;
		}


		//-----------------------------------------
		//
		//-----------------------------------------
		public void WriteCode(Int16 _code)
		{
			this.code = _code;
			//this.buffer = new byte[1024];

			// 헤더는 나중에 넣을것이므로 데이터 부터 넣을 수 있도록 위치를 점프시켜놓는다.
			this.position = Defines.HEADERSIZE;

			WriteShort(_code);
		}

		public void WriteSize()
		{
			Int16 _bodySize = (Int16)(this.position - Defines.HEADERSIZE);
			byte[] _header = BitConverter.GetBytes(_bodySize);
			_header.CopyTo(this.buffer, 0);
		}

		public void WriteByte(byte _data)
		{
			byte[] _tmpBuffer = BitConverter.GetBytes(_data);
			_tmpBuffer.CopyTo(this.buffer, this.position);
			this.position += sizeof(byte);
		}

		public void WriteShort(Int16 _data)
		{
			byte[] _tmpBuffer = BitConverter.GetBytes(_data);
			_tmpBuffer.CopyTo(this.buffer, this.position);
			this.position += _tmpBuffer.Length;
		}

		//public void push(Int16 _data)
		//{
		//	byte[] _tmpBuffer = BitConverter.GetBytes(_data);
		//	_tmpBuffer.CopyTo(this.buffer, this.position);
		//	this.position += _tmpBuffer.Length;
		//}

		public void WriteInt(Int32 _data)
		{
			byte[] _tmpBuffer = BitConverter.GetBytes(_data);
			_tmpBuffer.CopyTo(this.buffer, this.position);
			this.position += _tmpBuffer.Length;
		}

        public void WriteFloat(Single data)
        {
            byte[] _tmpBuffer = BitConverter.GetBytes(data);
            _tmpBuffer.CopyTo(this.buffer, this.position);
            this.position += _tmpBuffer.Length;
        }

		public void WriteString(string _data)
		{
			byte[] _tmpBuffer = Encoding.UTF8.GetBytes(_data);
			Int16 len = (Int16)_tmpBuffer.Length;

			byte[] len_buffer = BitConverter.GetBytes(len);
			len_buffer.CopyTo(this.buffer, this.position);
			this.position += sizeof(Int16);

			_tmpBuffer.CopyTo(this.buffer, this.position);
			this.position += _tmpBuffer.Length;
		}
	}
}
