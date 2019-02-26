using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeNet;

namespace CSampleServer
{
    using LoginProtocol;
	class Program
	{
        public static List<CRoom> listRoom;
		public static List<CGameUser> listGameUser;
		public static int roomIdentity = 1;

		static void Main(string[] args)
		{
			Console.WriteLine("Program Main");
			CPacketBufferManager.Initialize(2000);
			listGameUser	= new List<CGameUser>();
            listRoom		= new List<CRoom>();

            CNetworkService service = new CNetworkService();			
			service.onSessionCreated += OnSessionCreated;   // 콜백 매소드 설정.															
			service.Initialize();                           // 초기화.
			service.Listen("0.0.0.0", 49494, 100);


			Console.WriteLine("Started!");
			while (true)
			{
				//Console.Write(".");
				System.Threading.Thread.Sleep(1000);
			}
			//Console.Write("Server End");
			//Console.ReadKey();
		}

		/// <summary>
		/// 클라이언트가 접속 완료 하였을 때 호출됩니다.
		/// n개의 워커 스레드에서 호출될 수 있으므로 공유 자원 접근시 동기화 처리를 해줘야 합니다.
		/// </summary>
		/// <returns></returns>
		static void OnSessionCreated(CUserToken _token)
		{
			CGameUser _user = new CGameUser(_token);
			lock (listGameUser)
			{
				listGameUser.Add(_user);
			}
		}

		public static void RemoveUser(CGameUser user)
		{
            if (user.myRoom != null)
            {
                ExitUserRoom(user);
            }
            lock (listGameUser)
			{
				listGameUser.Remove(user);
			}
		}
        static void ExitUserRoom(CGameUser _user)
        {
            CPacket _response2 = CPacket.Create((short)LOGIN_PROTO.ROOM_EXIT_OTHER);
            _response2.WriteInt(_user.m_SN);
            _user.myRoom.BroadCast(_response2, _user);

            _user.myRoom.listGameUser.Remove(_user);
            --_user.myRoom.playerCount;
            if (_user.myRoom.playerCount <= 0)
            {
                Console.WriteLine(string.Format("delRoom {0}", _user.myRoom.name));
                RoomRemove(_user.myRoom);
            }
        }
        public static void Exit_Room(CGameUser user)
        {
            if (user.myRoom != null)
            {
                ExitUserRoom(user);
            }
        }


        public static bool RoomConnect(CGameUser _user, int _roomNum)
		{
			Console.WriteLine("Program RoomConnect _user:{0} _roomNum:{1}", _user, _roomNum);
			if (listRoom.Count == 0)
                return false;

            bool _connect;
            lock (listRoom)
            {
                _connect = listRoom[_roomNum].UserInsert(_user);
            }

            if(_connect)
                _user.SetRoom(listRoom[_roomNum]);

            return _connect;
        }

        public static void RoomCreate(CGameUser _user, string _roomName)
        {
			Console.WriteLine("Program RoomCreate _user:{0} _name:{1}", _user, _roomName);

            CRoom _room = new CRoom();
            _room.name = _roomName;
            _room.UserInsert(_user);
            _user.SetRoom(_room);
            lock (listRoom)
            {
                listRoom.Add(_room);

				//Room number 받아오는 부분에 버그가 있음...
				//1 -> 2 후에...
				//1 or 2제거된후에....
				//받아오면~~~~~ 2
				//2 2 번이된다 오류임....~~~
				//_room.number = listRoom.Count();
				_room.number = roomIdentity;
				roomIdentity++;
			}
        }

        public static void RoomRemove(CRoom _room)
        {
			Console.WriteLine("Program RoomRemove _room:{0}", _room);
            lock (listRoom)
            {
                listRoom.Remove(_room);
            }
        }
    }
}
