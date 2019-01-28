using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoginProtocol
{
    public enum LOGIN_PROTO : short
    {
        CREATE_ROOM_REQ, //방 만들기 : string 방이름
        CREATE_ROOM_OK, //성공
        CREATE_ROOM_FAILED, //실패
        
        ROOM_LIST_REQ,//방 목록 요청
        ROOM_LIST_ACK,//방 목록 응답, int 방번호
        ROOM_EXIT_REQ,//방 나감

        ROOM_CONNECT_REQ,//방 접속시도,int 방번호
        ROOM_CONNECT_OK,//접속 성공,내 번호, 유저번호
        ROOM_CONNECT_FAILED,//접속 실패

        ROOM_CONNECT_OTHER,//다른 유저 방 접속
        ROOM_EXIT_OTHER,//다른 유저 방 나감
        
        MOVING_USER_REQ,//SN이랑 위치정보

        HEART_SEND_REQ, //하트 전송, SN

        LOGIN_REQUEST,
        LOGIN_REPLY,
        FAILD,
        OK,
        MOVE,
        ATTACK,
        USER_CONNECT,
        CHAT_MSG_REQ,

        END
    }
}