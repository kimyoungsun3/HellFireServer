using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreeNet;

namespace CSampleServer
{
	class CRoom
	{
        public string RoomName = "Default";


        public int RoomNum = 0;
        public int PlayerNumMax = 8;
        public int CurrentPlayerNum = 0;

        public List<CGameUser> UserInfoList = new  List<CGameUser>();


        public void BroadCast(CPacket msg,CGameUser Sender)
        {
            List<CGameUser> UserList = UserInfoList;
            foreach (CGameUser client in UserList)
            {
                if (client.token != Sender.token)
                    client.token.Send(msg);
            }
            //this.token.send(msg);
        }
        int RoomuserSn = 0;
        public bool UserInsert(CGameUser _user)
        {
            if (CurrentPlayerNum < PlayerNumMax)
            {
                UserInfoList.Add(_user);
                _user.m_SN = RoomuserSn;
                ++RoomuserSn;
                ++CurrentPlayerNum;
                return true;
            }
            return false;
        }
	}
}
