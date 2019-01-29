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
			Console.WriteLine(this + " ParseCode buffer:{0}", _buffer);
			// ex)
			CPacket _packet		= new CPacket(_buffer.Value, this);
            LOGIN_PROTO _code	= (LOGIN_PROTO)_packet.pop_protocol_id();
			Console.WriteLine(" > _code:" + _code);

			switch (_code)
			{
                case LOGIN_PROTO.CREATE_ROOM_REQ:
                    {
						Console.WriteLine("[C -> S] CREATE_ROOM_REQ");
                        if(myRoom == null)
                        {
                            bRoomMaster = true; //방장이여

                            string _str = _packet.ReadString();
                            Console.WriteLine(" > {0} > CreateRoom", _str);
                            Program.RoomCreate(this, _str);
                            CPacket _response = CPacket.Create((short)LOGIN_PROTO.CREATE_ROOM_OK);
                            _response.WriteInt(m_SN);
                            SendCode(_response); //잘만들었으
                        }
                        else
                            SendCode(CPacket.Create((short)LOGIN_PROTO.CREATE_ROOM_FAILED)); //이미 접속한 방이있어
                    }
                    break;
                case LOGIN_PROTO.ROOM_LIST_REQ:
					{
						Console.WriteLine("[C -> S] ROOM_LIST_REQ");

						CPacket _response = CPacket.Create((short)LOGIN_PROTO.ROOM_LIST_ACK);
                        int count = Program.listRoom.Count;
                        _response.WriteInt(count);
                        for (int i = 0; i < count; i++) // Loop with for.
                        {
                            if (Program.listRoom[i] != null)
                            {
                                _response.WriteString(Program.listRoom[i].name);
								_response.WriteInt(Program.listRoom[i].playerCount);
								_response.WriteInt(Program.listRoom[i].playerMax);
							}
                        }
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
						int roomnum = _packet.ReadInt();

                        if(Program.RoomConnect(this, roomnum))
                        {
                                                            //성공
                            CPacket _response = CPacket.Create((short)LOGIN_PROTO.ROOM_CONNECT_OK);

                            _response.WriteInt(myRoom.listGameUser.Count);
                            _response.WriteInt(m_SN);
                            for (int i = 0; i < myRoom.listGameUser.Count; ++i )
                            {
                                _response.WriteInt(myRoom.listGameUser[i].m_SN);
                            }
                            SendCode(_response); //잘만들었으

                            CPacket _response2 = CPacket.Create((short)LOGIN_PROTO.ROOM_CONNECT_OTHER);
                            _response2.WriteInt(m_SN);
                            myRoom.BroadCast(_response2, this);
                        }
                        else
                        {
                            //실패
                            SendCode(CPacket.Create((short)LOGIN_PROTO.ROOM_CONNECT_FAILED));
                        }
                    }
                    break;
                case LOGIN_PROTO.CHAT_MSG_REQ:
					{
						Console.WriteLine("[C -> S] CHAT_MSG_REQ");
						if (myRoom != null)
                        {
                            string text = _packet.ReadString();
                            Console.WriteLine(string.Format("text {0}", text));

                            CPacket _response = CPacket.Create((short)LOGIN_PROTO.CHAT_MSG_REQ);
                            _response.WriteInt(m_SN);
                            _response.WriteString(text);
                            myRoom.BroadCast(_response, this);
                        }
                        //BroadCast(response);
                    }
                    break;
                case LOGIN_PROTO.LOGIN_REQUEST:
					{
						Console.WriteLine("[C -> S] LOGIN_REQUEST");
						//msg.pop_string();


						UserInfo NewUser = new UserInfo();
						NewUser.Name = _packet.ReadString();
						NewUser.SN = m_SN;
						listUserInfo.Add(NewUser);
						Console.WriteLine("Connect" + NewUser.Name);

						CPacket _response1 = CPacket.Create((short)LOGIN_PROTO.LOGIN_REPLY);
						_response1.WriteInt(m_SN);
						_response1.WriteString(NewUser.Name);
						BroadCast(_response1);


						CPacket _response = CPacket.Create((short)LOGIN_PROTO.USER_CONNECT);
						_response.WriteInt(m_SN);
						_response.WriteString(NewUser.Name);
						SendCode(_response);

						for (int i = 0; i < listUserInfo.Count; i++) // Loop with for.
						{
							if (m_SN != listUserInfo[i].SN)
							{
								CPacket _response3 = CPacket.Create((short)LOGIN_PROTO.LOGIN_REPLY);
								_response3.WriteInt(listUserInfo[i].SN);
								_response3.WriteString(listUserInfo[i].Name);
								SendCode(_response3);
							}
						}

						++m_SN;
					}
					break;

                case LOGIN_PROTO.MOVING_USER_REQ:
					{
						Console.WriteLine("[C -> S] MOVING_USER_REQ");
						int SN = _packet.ReadInt();
						float x = _packet.ReadFloat();
						float y = _packet.ReadFloat();
						float z = _packet.ReadFloat();
						BroadCast(_packet);
						Console.WriteLine(x + ":" + y + ":" + z);
					}
					break;
                case LOGIN_PROTO.ATTACK:
					{
						Console.WriteLine("[C -> S] ATTACK");
						int SN = _packet.ReadInt();
						byte Type = _packet.ReadByte();
						float x = _packet.ReadFloat();
						float y = _packet.ReadFloat();
						float z = _packet.ReadFloat();
						float w = _packet.ReadFloat();
						BroadCast(_packet);
					}
					break;
				default:
					{
						Console.WriteLine("[C -> S] ####(프로토콜이 지정되지 않았어요.) " + (LOGIN_PROTO) _code);
					}
					break;
			}
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
