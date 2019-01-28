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
        public static List<CRoom> roomlist;
		public static List<CGameUser> userlist;

		static void Main(string[] args)
		{
			Console.WriteLine("Program Main");
			CPacketBufferManager.Initialize(2000);
			userlist = new List<CGameUser>();
            roomlist = new List<CRoom>();

            CNetworkService service = new CNetworkService();
			// 콜백 매소드 설정.
			service.onSessionCreated += OnSessionCreated;
			// 초기화.
			service.Initialize();
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
			lock (userlist)
			{
				userlist.Add(_user);
			}
		}

		public static void remove_user(CGameUser user)
		{
            if (user.m_myRoom != null)
            {
                ExitUserRoom(user);
            }
            lock (userlist)
			{
				userlist.Remove(user);
			}
		}
        static void ExitUserRoom(CGameUser user)
        {
            CPacket response2 = CPacket.Create((short)LOGIN_PROTO.ROOM_EXIT_OTHER);
            response2.push(user.m_SN);
            user.m_myRoom.BroadCast(response2, user);

            user.m_myRoom.UserInfoList.Remove(user);
            --user.m_myRoom.CurrentPlayerNum;
            if (user.m_myRoom.CurrentPlayerNum <= 0)
            {
                Console.WriteLine(string.Format("delRoom {0}", user.m_myRoom.RoomName));
                remove_room(user.m_myRoom);
            }
        }
        public static void Exit_Room(CGameUser user)
        {
            if (user.m_myRoom != null)
            {
                ExitUserRoom(user);
            }
        }


        public static bool room_connect(CGameUser user, int Roomnum)
        {
            if (roomlist.Count == 0)
                return false;

            bool Isconnect;
            lock (roomlist)
            {
                Isconnect = roomlist[Roomnum].UserInsert(user);
            }
            if(Isconnect)
                user.SetRoom(roomlist[Roomnum]);

            return Isconnect;
        }

        public static void room_create(CGameUser user,string Name)
        {
            CRoom newRoom = new CRoom();
            newRoom.RoomName = Name;
            newRoom.UserInsert(user);
            user.SetRoom(newRoom);
            lock (roomlist)
            {
                roomlist.Add(newRoom);
                newRoom.RoomNum = roomlist.Count();
            }
        }

        public static void remove_room(CRoom room)
        {
            lock (roomlist)
            {
                roomlist.Remove(room);
            }
        }
    }
}
