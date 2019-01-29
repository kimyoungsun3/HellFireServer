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
        public string name = "Default";
        public int number = 0;
        public int playerMax = 8;
        public int playerCount = 0;

        public List<CGameUser> listGameUser = new  List<CGameUser>();

        public void BroadCast(CPacket _packet, CGameUser _senderClient)
        {
            foreach (CGameUser _client in listGameUser)
            {
                if (_client.token != _senderClient.token)
                    _client.token.SendCode(_packet);
            }
            //this.token.send(msg);
        }
        int RoomuserSn = 0;
        public bool UserInsert(CGameUser _user)
        {
            if (playerCount < playerMax)
            {
                listGameUser.Add(_user);
                _user.m_SN = RoomuserSn;
                ++RoomuserSn;
                ++playerCount;
                return true;
			}
			else
			{
				return false;
			}            
        }
	}
}
