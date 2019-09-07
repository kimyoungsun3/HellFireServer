using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeNet;

namespace CSampleServer
{
    using LoginProtocol;

    public struct UserInfo
    {
        public int SN;
        public string Name;
        public float PosX;
        public float PosY;
        public float PosZ;
    };
	/// <summary>
	/// 하나의 session객체를 나타낸다.
	/// </summary>
	class CGameUser : IPeer
	{
	    public CUserToken token;
        public int m_SN = 0;
        static List<UserInfo> listUserInfo = new  List<UserInfo>();

		public CGameUser(CUserToken _token)
		{
			Console.WriteLine(this + " Constructor _token:{0}", _token);
			this.token = _token;
			this.token.SetPeer(this);

            //CPacket response = CPacket.create((short)LOGIN_PROTO.USER_CONNECT);
            //response.push(m_SN);
            //send(response);
            //++m_SN;
		}

        bool bRoomMaster = false; //방장 여부 true면 방장
        public CRoom myRoom = null;//내가 접속한 방
        public void SetRoom(CRoom _myRoom)
		{
			Console.WriteLine(this + " SetRoom _myRoom:{0}", _myRoom);
			myRoom = _myRoom;
        }

		//void IPeer.ParseCode(Const<byte[]> _buffer)
		public void ParseCode(Const<byte[]> _buffer)
		{
			Console.WriteLine("======================================");
			Console.WriteLine(this + " 패킷 들어옴... 파싱하기. ParseCode");
			Console.WriteLine("======================================");
			//_packet은 함수 안에서만 유효한것임... 다른데에서는 사용안함...
			CPacket _receive	= new CPacket(_buffer.Value, this);
            LOGIN_PROTO _code	= (LOGIN_PROTO)_receive.pop_protocol_id();
			Console.WriteLine(" > _code:" + _code);

			switch (_code)
			{
                case LOGIN_PROTO.CREATE_ROOM_REQ:
                    {
						Console.WriteLine("[C -> S] CREATE_ROOM_REQ");
                        if(myRoom == null)
                        {
                            bRoomMaster = true; //방장이여

                            string _strRoomName = _receive.ReadString();
                            Program.RoomCreate(this, _strRoomName);
							Console.WriteLine(" > _strRoomName:{0}", _strRoomName);

							Console.WriteLine("[C <- S] CREATE_ROOM_OK");
							CPacket _response = CPacket.Create((short)LOGIN_PROTO.CREATE_ROOM_OK);
                            _response.WriteInt(m_SN);
                            SendCode(_response);
                        }
						else
						{
							Console.WriteLine("[C <- S] CREATE_ROOM_FAILED");
							SendCode(CPacket.Create((short)LOGIN_PROTO.CREATE_ROOM_FAILED)); //이미 접속한 방이있어
						}
                    }
                    break;
                case LOGIN_PROTO.ROOM_LIST_REQ:
					{
						Console.WriteLine("[C -> S] ROOM_LIST_REQ");

						CPacket _response = CPacket.Create((short)LOGIN_PROTO.ROOM_LIST_ACK);
                        int _countRoom = Program.listRoom.Count;
						_response.WriteInt(_countRoom);

						Console.WriteLine(" > 방수량:{0}", _countRoom);
						for (int i = 0; i < _countRoom; i++) // Loop with for.
                        {
                            if (Program.listRoom[i] != null)
                            {
                                _response.WriteString(Program.listRoom[i].name);
								_response.WriteInt(Program.listRoom[i].playerCount);
								_response.WriteInt(Program.listRoom[i].playerMax);
							}
						}
						Console.WriteLine("[C <- S] ROOM_LIST_ACK");
						SendCode(_response);
                    }
                    break;

                case LOGIN_PROTO.ROOM_EXIT_REQ:
					{
						Console.WriteLine("[C -> S] ROOM_EXIT_REQ");
						Program.Exit_Room(this);
                        myRoom = null;
                    }
                    break;
                    
                case LOGIN_PROTO.ROOM_CONNECT_REQ:
					{
						Console.WriteLine("[C -> S] ROOM_CONNECT_REQ");
						int _roomNum = _receive.ReadInt();

                        if(Program.RoomConnect(this, _roomNum))
                        {
                            //성공
                            CPacket _response = CPacket.Create((short)LOGIN_PROTO.ROOM_CONNECT_OK);

                            _response.WriteInt(myRoom.listGameUser.Count);
                            _response.WriteInt(m_SN);
                            for (int i = 0; i < myRoom.listGameUser.Count; ++i )
                            {
                                _response.WriteInt(myRoom.listGameUser[i].m_SN);
							}
							Console.WriteLine("[C <- S] ROOM_CONNECT_OK");
							SendCode(_response); //잘만들었으

                            CPacket _response2 = CPacket.Create((short)LOGIN_PROTO.ROOM_CONNECT_OTHER);
                            _response2.WriteInt(m_SN);
							Console.WriteLine("[C <- S] ROOM_CONNECT_OTHER");
							myRoom.BroadCast(_response2, this);
                        }
                        else
                        {
							//실패
							Console.WriteLine("[C <- S] ROOM_CONNECT_FAILED");
							SendCode(CPacket.Create((short)LOGIN_PROTO.ROOM_CONNECT_FAILED));
                        }
                    }
                    break;
                case LOGIN_PROTO.CHAT_MSG_REQ:
					{
						Console.WriteLine("[C -> S] CHAT_MSG_REQ");
						string _strMsg = _receive.ReadString();
						Console.WriteLine(" > _strMsg:{0}", _strMsg);
						if (myRoom != null)
                        {                           

                            CPacket _response = CPacket.Create((short)LOGIN_PROTO.CHAT_MSG_REQ);
                            _response.WriteInt(m_SN);
                            _response.WriteString(_strMsg);
							Console.WriteLine("[C <- S] CHAT_MSG_REQ");
							myRoom.BroadCast(_response, this);
						}
						else
						{
							CPacket _response = CPacket.Create((short)LOGIN_PROTO.CHAT_MSG_REQ);
							_response.WriteInt(0);
							_response.WriteString(_strMsg);
							Console.WriteLine("[C <- S] CHAT_MSG_REQ");
							SendCode(_response);
						}
                    }
                    break;
                case LOGIN_PROTO.LOGIN_REQUEST:
					{
						Console.WriteLine("[C -> S] LOGIN_REQUEST");
						//msg.pop_string();

						UserInfo _userInfo = new UserInfo();
						_userInfo.Name = _receive.ReadString();
						_userInfo.SN = m_SN;
						listUserInfo.Add(_userInfo);
						//Console.WriteLine(" Connect" + _userInfo.Name);

						CPacket _response1 = CPacket.Create((short)LOGIN_PROTO.LOGIN_REPLY);
						_response1.WriteInt(m_SN);
						_response1.WriteString(_userInfo.Name);
						Console.WriteLine("[C <- S] LOGIN_REPLY");
						BroadCast(_response1);


						CPacket _response = CPacket.Create((short)LOGIN_PROTO.USER_CONNECT);
						_response.WriteInt(m_SN);
						_response.WriteString(_userInfo.Name);
						Console.WriteLine("[C <- S] USER_CONNECT");
						SendCode(_response);

						for (int i = 0; i < listUserInfo.Count; i++) // Loop with for.
						{
							if (m_SN != listUserInfo[i].SN)
							{
								CPacket _response3 = CPacket.Create((short)LOGIN_PROTO.LOGIN_REPLY);
								_response3.WriteInt(listUserInfo[i].SN);
								_response3.WriteString(listUserInfo[i].Name);
								Console.WriteLine("[C <- S] LOGIN_REPLY");
								SendCode(_response3);
							}
						}

						++m_SN;
					}
					break;
                case LOGIN_PROTO.MOVING_USER_REQ:
					{
						Console.WriteLine("[C -> S] MOVING_USER_REQ");
						int SN = _receive.ReadInt();
						float x = _receive.ReadFloat();
						float y = _receive.ReadFloat();
						float z = _receive.ReadFloat();
						Console.WriteLine(" > SN:{0} x:{1} y:{2}, z:{3}", SN, x, y, z);
						Console.WriteLine("[C <- S] MOVING_USER_REQ");

						BroadCast(_receive);
					}
					break;
				case LOGIN_PROTO.MOVING_USER_ONLY:
					{
						Console.WriteLine("[C -> S] MOVING_USER_ONLY");
						int SN = _receive.ReadInt();
						float x = _receive.ReadFloat();
						float y = _receive.ReadFloat();
						float z = _receive.ReadFloat();
						Console.WriteLine(" > SN:{0} x:{1} y:{2}, z:{3}", SN, x, y, z);
						Console.WriteLine("[C <- S] MOVING_USER_ONLY");


						CPacket _response = CPacket.Create((short)LOGIN_PROTO.MOVING_USER_ONLY);
						_response.WriteInt(SN);
						_response.WriteFloat(x);
						_response.WriteFloat(y);
						_response.WriteFloat(z);
						
						List<CGameUser> _userList = Program.listGameUser;
						foreach (CGameUser _client in _userList)
						{
							_client.token.SendCode(_response);
						}
					}
					break;
				case LOGIN_PROTO.ATTACK:
					{
						Console.WriteLine("[C -> S] ATTACK");
						int SN = _receive.ReadInt();
						byte Type = _receive.ReadByte();
						float x = _receive.ReadFloat();
						float y = _receive.ReadFloat();
						float z = _receive.ReadFloat();
						float w = _receive.ReadFloat();
						Console.WriteLine(" > SN:{0} Type:{1} x:{2}, y:{3}, z:{4}, w:{5}", SN, Type, x, y, z, w);
						Console.WriteLine("[C <- S] ATTACK");
						BroadCast(_receive);
					}
					break;
				case LOGIN_PROTO.PTC_ECHO:
					{
						Console.WriteLine("[C -> S] PTC_ECHO");
						string _strMsg = _receive.ReadString();
						Console.WriteLine(" > _strMsg:{0}", _strMsg);

						CPacket _response = CPacket.Create((short)LOGIN_PROTO.PTS_ECHO);
						_response.WriteString(_strMsg);
						Console.WriteLine("[C <- S] PTS_ECHO");
						SendCode(_response);
					}
					break;
				default:
					{
						Console.WriteLine("[C -> S] ####(프로토콜이 지정되지 않았어요.) " + (LOGIN_PROTO) _code);
					}
					break;
			}

			//사용하고 남은 것은 해제해준다.
			//CPacket.Destroy(_packet);
		}

		//void IPeer.OnRemoved()
		public void OnRemoved()
		{
			Console.WriteLine(this + " OnRemoved");
			Program.RemoveUser(this);
		}

		public void SendCode(CPacket _packet)
		{
			Console.WriteLine(this + " SendCode _packet:{0}", _packet);
			this.token.SendCode(_packet);
		}

        public void BroadCast(CPacket _packet)
		{
			Console.WriteLine(this + " BroadCast:{0}", _packet);
			List<CGameUser> _userList = myRoom.listGameUser;
			foreach (CGameUser _client in _userList)   
			{
				if (_client.token != this.token)
					_client.token.SendCode(_packet);
			}
           //this.token.send(msg);
        }

		//void IPeer.Disconnect()
		public void Disconnect()
		{
			Console.WriteLine(this + " Disconnect");
			this.token.socket.Disconnect(false);
		}

		////void IPeer.process_user_operation(CPacket _packet)
		//{
		//	Console.WriteLine(this + " IPeer.process_user_operation _packet:{0}", _packet);
		//}
	}
}
