// Guids.cs
// MUST match guids.h

using System;

namespace Aderant.DeveloperTools
{
    static class GuidList
    {
        public const string guidVSPackage2PkgString = "b36002e4-cf03-4ed9-9f5c-bf15991e15e4";
        public const string guidVSPackage2CmdSetString = "c74273b4-c175-4464-b05c-a783ccc354c0";

        public static readonly Guid guidVSPackage2CmdSet = new Guid(guidVSPackage2CmdSetString);
    };
}