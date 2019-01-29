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

        bool RoomMaster = false; //방장 여부 true면 방장
        public CRoom m_myRoom = null;//내가 접속한 방
        public void SetRoom(CRoom _myRoom)
		{
			Console.WriteLine(this + " SetRoom _myRoom:{0}", _myRoom);
			m_myRoom = _myRoom;
        }

		void IPeer.on_message(Const<byte[]> _buffer)
		{
			Console.WriteLine(this + " IPeer.on_message buffer:{0}", _buffer);
			// ex)
			CPacket _packet		= new CPacket(_buffer.Value, this);
            LOGIN_PROTO _code	= (LOGIN_PROTO)_packet.pop_protocol_id();

			Console.WriteLine("protocol id " + _code);
			switch (_code)
			{
                case LOGIN_PROTO.CREATE_ROOM_REQ:
                    {
						Console.WriteLine("[C -> S] CREATE_ROOM_REQ");
                        if(m_myRoom == null)
                        {
                            RoomMaster = true; //방장이여

                            string text = _packet.ReadString();
                            Console.WriteLine(string.Format("newRoom {0}", text));
                            Program.room_create(this, text);
                            CPacket _response = CPacket.Create((short)LOGIN_PROTO.CREATE_ROOM_OK);
                            _response.WriteInt(m_SN);
                            send(_response); //잘만들었으
                        }
                        else
                            send(CPacket.Create((short)LOGIN_PROTO.CREATE_ROOM_FAILED)); //이미 접속한 방이있어
                    }
                    break;
                case LOGIN_PROTO.ROOM_LIST_REQ:
					{
						Console.WriteLine("[C -> S] ROOM_LIST_REQ");

						CPacket _response = CPacket.Create((short)LOGIN_PROTO.ROOM_LIST_ACK);
                        int count = Program.roomlist.Count;
                        _response.WriteInt(count);
                        for (int i = 0; i < count; i++) // Loop with for.
                        {
                            if (Program.roomlist[i] != null)
                            {
                                _response.WriteString(Program.roomlist[i].RoomName);
								_response.WriteInt(Program.roomlist[i].CurrentPlayerNum);
								_response.WriteInt(Program.roomlist[i].PlayerNumMax);
							}
                        }
                        send(_response);
                    }
                    break;

                case LOGIN_PROTO.ROOM_EXIT_REQ:
					{
						Console.WriteLine("[C -> S] ROOM_EXIT_REQ");
						Program.Exit_Room(this);
                        m_myRoom = null;
                    }
                    break;
                    
                case LOGIN_PROTO.ROOM_CONNECT_REQ:
					{
						Console.WriteLine("[C -> S] ROOM_CONNECT_REQ");
						int roomnum = _packet.ReadInt();

                        if(Program.room_connect(this, roomnum))
                        {
                                                            //성공
                            CPacket _response = CPacket.Create((short)LOGIN_PROTO.ROOM_CONNECT_OK);

                            _response.WriteInt(m_myRoom.UserInfoList.Count);
                            _response.WriteInt(m_SN);
                            for (int i = 0; i < m_myRoom.UserInfoList.Count; ++i )
                            {
                                _response.WriteInt(m_myRoom.UserInfoList[i].m_SN);
                            }
                            send(_response); //잘만들었으

                            CPacket _response2 = CPacket.Create((short)LOGIN_PROTO.ROOM_CONNECT_OTHER);
                            _response2.WriteInt(m_SN);
                            m_myRoom.BroadCast(_response2, this);
                        }
                        else
                        {
                            //실패
                            send(CPacket.Create((short)LOGIN_PROTO.ROOM_CONNECT_FAILED));
                        }
                    }
                    break;
                case LOGIN_PROTO.CHAT_MSG_REQ:
					{
						Console.WriteLine("[C -> S] CHAT_MSG_REQ");
						if (m_myRoom != null)
                        {
                            string text = _packet.ReadString();
                            Console.WriteLine(string.Format("text {0}", text));

                            CPacket _response = CPacket.Create((short)LOGIN_PROTO.CHAT_MSG_REQ);
                            _response.WriteInt(m_SN);
                            _response.WriteString(text);
                            m_myRoom.BroadCast(_response, this);
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
						send(_response);

						for (int i = 0; i < listUserInfo.Count; i++) // Loop with for.
						{
							if (m_SN != listUserInfo[i].SN)
							{
								CPacket _response3 = CPacket.Create((short)LOGIN_PROTO.LOGIN_REPLY);
								_response3.WriteInt(listUserInfo[i].SN);
								_response3.WriteString(listUserInfo[i].Name);
								send(_response3);
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
						Console.WriteLine("[C -> S] ####(미지정) " + (LOGIN_PROTO) _code);
					}
					break;
			}
		}

		void IPeer.OnRemoved()
		{
			Console.WriteLine(this + " IPeer.OnRemoved");
			Program.remove_user(this);
		}

		public void send(CPacket _packet)
		{
			Console.WriteLine(this + " send _packet:{0}", _packet);
			this.token.Send(_packet);
		}

        public void BroadCast(CPacket _packet)
		{
			Console.WriteLine(this + " BroadCast:{0}", _packet);
			List<CGameUser> UserList = m_myRoom.UserInfoList;
           foreach (CGameUser _client in UserList)   
           {
               if (_client.token != this.token)
                   _client.token.Send(_packet);
           }
            //this.token.send(msg);
        }
		void IPeer.disconnect()
		{
			Console.WriteLine(this + " IPeer.disconnect");
			this.token.socket.Disconnect(false);
		}

		void IPeer.process_user_operation(CPacket _packet)
		{
			Console.WriteLine(this + " IPeer.process_user_operation _packet:{0}", _packet);
		}
	}
}
