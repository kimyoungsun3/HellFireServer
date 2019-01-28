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

                            string text = _packet.pop_string();
                            Console.WriteLine(string.Format("newRoom {0}", text));
                            Program.room_create(this, text);
                            CPacket response = CPacket.create((short)LOGIN_PROTO.CREATE_ROOM_OK);
                            response.push(m_SN);
                            send(response); //잘만들었으
                        }
                        else
                            send(CPacket.create((short)LOGIN_PROTO.CREATE_ROOM_FAILED)); //이미 접속한 방이있어
                    }
                    break;
                case LOGIN_PROTO.ROOM_LIST_REQ:
					{
						Console.WriteLine("[C -> S] ROOM_LIST_REQ");

						CPacket response = CPacket.create((short)LOGIN_PROTO.ROOM_LIST_ACK);
                        int count = Program.roomlist.Count;
                        response.push(count);
                        for (int i = 0; i < count; i++) // Loop with for.
                        {
                            if (Program.roomlist[i] != null)
                            {
                                response.push(Program.roomlist[i].RoomName);
								response.push(Program.roomlist[i].CurrentPlayerNum);
								response.push(Program.roomlist[i].PlayerNumMax);
							}
                        }
                        send(response);
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
						int roomnum = _packet.pop_int32();

                        if(Program.room_connect(this, roomnum))
                        {
                                                            //성공
                            CPacket response = CPacket.create((short)LOGIN_PROTO.ROOM_CONNECT_OK);

                            response.push(m_myRoom.UserInfoList.Count);
                            response.push(m_SN);
                            for (int i = 0; i < m_myRoom.UserInfoList.Count; ++i )
                            {
                                response.push(m_myRoom.UserInfoList[i].m_SN);
                            }
                            send(response); //잘만들었으

                            CPacket response2 = CPacket.create((short)LOGIN_PROTO.ROOM_CONNECT_OTHER);
                            response2.push(m_SN);
                            m_myRoom.BroadCast(response2, this);
                        }
                        else
                        {
                            //실패
                            send(CPacket.create((short)LOGIN_PROTO.ROOM_CONNECT_FAILED));
                        }
                    }
                    break;
                case LOGIN_PROTO.CHAT_MSG_REQ:
					{
						Console.WriteLine("[C -> S] CHAT_MSG_REQ");
						if (m_myRoom != null)
                        {
                            string text = _packet.pop_string();
                            Console.WriteLine(string.Format("text {0}", text));

                            CPacket response = CPacket.create((short)LOGIN_PROTO.CHAT_MSG_REQ);
                            response.push(m_SN);
                            response.push(text);
                            m_myRoom.BroadCast(response, this);
                        }
                        //BroadCast(response);
                    }
                    break;
                case LOGIN_PROTO.LOGIN_REQUEST:
					{
						Console.WriteLine("[C -> S] LOGIN_REQUEST");
						//msg.pop_string();


						UserInfo NewUser = new UserInfo();
						NewUser.Name = _packet.pop_string();
						NewUser.SN = m_SN;
						listUserInfo.Add(NewUser);
						Console.WriteLine("Connect" + NewUser.Name);

						CPacket response1 = CPacket.create((short)LOGIN_PROTO.LOGIN_REPLY);
						response1.push(m_SN);
						response1.push(NewUser.Name);
						BroadCast(response1);


						CPacket response = CPacket.create((short)LOGIN_PROTO.USER_CONNECT);
						response.push(m_SN);
						response.push(NewUser.Name);
						send(response);

						for (int i = 0; i < listUserInfo.Count; i++) // Loop with for.
						{
							if (m_SN != listUserInfo[i].SN)
							{
								CPacket response3 = CPacket.create((short)LOGIN_PROTO.LOGIN_REPLY);
								response3.push(listUserInfo[i].SN);
								response3.push(listUserInfo[i].Name);
								send(response3);
							}
						}

						++m_SN;
					}
					break;

                case LOGIN_PROTO.MOVING_USER_REQ:
					{
						Console.WriteLine("[C -> S] MOVING_USER_REQ");
						int SN = _packet.pop_int32();
						float x = _packet.pop_Single();
						float y = _packet.pop_Single();
						float z = _packet.pop_Single();
						BroadCast(_packet);
						Console.WriteLine(x + ":" + y + ":" + z);
					}
					break;
                case LOGIN_PROTO.ATTACK:
					{
						Console.WriteLine("[C -> S] ATTACK");
						int SN = _packet.pop_int32();
						byte Type = _packet.pop_byte();
						float x = _packet.pop_Single();
						float y = _packet.pop_Single();
						float z = _packet.pop_Single();
						float w = _packet.pop_Single();
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
