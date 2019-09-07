using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FreeNet
{
	public class CPacketBufferManager
	{
		static object tmpObject = new object();
		static Stack<CPacket> pool;
		static int poolCapacity;
		public static int GetCount() { return pool.Count; }
		public static void Initialize(int _capacity)
		{
			if (Constant.DEBUG)
				Console.WriteLine("CPacketBufferManager Initialize capacity:" + _capacity);
			pool			= new Stack<CPacket>();
			poolCapacity	= _capacity;
			Allocate();
		}

		private static void Allocate()
		{
			for (int i = 0; i < poolCapacity; ++i)
			{
				pool.Push(new CPacket());
			}
		}

		public static CPacket Pop()
		{
			lock (tmpObject)
			{
				if (pool.Count <= 0)
				{
					Console.WriteLine("reallocate.");
					Allocate();
				}
				return pool.Pop();
			}
		}

		public static void Push(CPacket _packet)
		{
			lock(tmpObject)
			{
				//Console.WriteLine(" >> *** packet restore:" + _packet.identity);
				pool.Push(_packet);
			}
		}
	}
}
