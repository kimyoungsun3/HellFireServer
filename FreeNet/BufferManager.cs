using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace FreeNet
{
    /// <summary>
    /// This class creates a single large buffer which can be divided up and assigned to SocketAsyncEventArgs objects for use
    /// with each socket I/O operation.  This enables bufffers to be easily reused and gaurds against fragmenting heap memory.
    /// 
    /// The operations exposed on the BufferManager class are not thread safe.
    /// </summary>
    internal class BufferManager
    {

        int bufferTotalSize;                 // the total number of bytes controlled by the buffer pool
        Stack<int> freeIndexPool;     // 
		byte[] buffer;                // the underlying byte array maintained by the Buffer Manager
		int bufferOffset;
        int bufferSize;

        public BufferManager(int _bufferTotalSize, int _bufferSize)
        {
            bufferTotalSize	= _bufferTotalSize;
            bufferOffset	= 0;
            this.bufferSize	= _bufferSize;
            freeIndexPool	= new Stack<int>();
        }

        /// <summary>
        /// Allocates buffer space used by the buffer pool
        /// </summary>
        public void InitBuffer()
        {
            // create one big large buffer and divide that out to each SocketAsyncEventArg object
            buffer = new byte[bufferTotalSize];
        }

        /// <summary>
        /// Assigns a buffer from the buffer pool to the specified SocketAsyncEventArgs object
        /// </summary>
        /// <returns>true if the buffer was successfully set, else false</returns>
        public bool SetBuffer(SocketAsyncEventArgs _args)
        {
            if (freeIndexPool.Count > 0)
            {
                _args.SetBuffer(buffer, freeIndexPool.Pop(), bufferSize);
            }
            else
            {
                if ((bufferTotalSize - bufferSize) < bufferOffset)
                {
                    return false;
                }
                _args.SetBuffer(buffer, bufferOffset, bufferSize);
                bufferOffset += bufferSize;
            }
            return true;
        }

        /// <summary>
        /// Removes the buffer from a SocketAsyncEventArg object.  This frees the buffer back to the 
        /// buffer pool
        /// </summary>
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }
}
