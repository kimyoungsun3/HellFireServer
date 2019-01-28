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

		public static void Initialize(int _capacity)
		{
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

		public static void Push(CPacket packet)
		{
			lock(tmpObject)
			{
				pool.Push(packet);
			}
		}
	}
}
