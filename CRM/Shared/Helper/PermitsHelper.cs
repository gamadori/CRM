using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Helper
{
    public static class PermitsHelper
    {
        public const int BitRead = 0x0001;

        public const int BitEdit = 0x0002;

        public const int BitInsert = 0x0004;

        public const int BitDelete = 0x0008;

        public const int BitAssign = 0x0100;

        public const int BitClose = 0x0200;

        public const int BitReOpen = 0x0400;

        public const int BitInternalData = 0x1000;

        public static bool CanRead(int permits)
        {
            return (permits & BitRead) == BitRead;
        }

        public static bool CanEdit(int permits)
        {
            return (permits & BitEdit) == BitEdit;
        }

        public static bool CanInsert(int permits)
        {
            return (permits & BitInsert) == BitInsert;
        }

        public static bool CanDelete(int permits)
        {
            return ((permits & BitDelete) == BitDelete);
        }

        public static bool CanAssign(int permits)
        {
            return ((permits & BitAssign) == BitAssign);
        }

        public static bool CanClose(int permits)
        {
            return (((permits & BitClose) == BitClose));
        }

        public static bool CanReOpen(int permits)
        {
            return (((permits & BitReOpen) == BitReOpen));
        }

        public static bool CanInternalData(int permits)
        {
            return (((permits & BitInternalData) == BitInternalData));
        }

        public static int SetRead(int permits)
        {
            return permits | BitRead;
        }

        public static int SetEdit(int permits)
        {
            return permits | BitEdit;
        }

        public static int ResEdit(int permits)
        {
            return permits & ~BitEdit;
        }

        public static int SetInsert(int permits)
        {
            return permits | BitInsert;
        }

        public static int SetDelete(int permits)
        {
            return permits | BitDelete;
        }

        public static int SetAssign(int permits)
        {
            return permits | BitAssign;
        }

        public static int SetClose(int permits)
        {
            return permits | BitClose;
        }

        public static int SetReOpen(int permits)
        {
            return permits | BitReOpen;
        }

        public static int SetInternalData(int permits)
        {
            return permits | BitInternalData;

        }

        public static bool Read(this int permits) => CanRead(permits);

        public static bool Edit(this int permits) => CanEdit(permits);

        public static bool Insert(this int permits) => CanInsert(permits);

        public static bool Delete(this int permits) => CanDelete(permits);

        public static bool Assign(this int permits) => CanAssign(permits);

        public static bool Close(this int permits) => CanClose(permits);

        public static bool ReOpen(this int permits) => CanReOpen(permits);

        public static bool InternalData(this int permits) => CanInternalData(permits);
    }
}
