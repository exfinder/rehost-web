// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Owin.Host.SystemWeb.CallEnvironment
{
    internal class TraceTextWriter : TextWriter
    {
        internal static TraceTextWriter Instance = new TraceTextWriter();

        public TraceTextWriter()
            : base(CultureInfo.InvariantCulture)
        {
        }

        public override Encoding Encoding
        {
            get { return Encoding.Default; }
        }

        public override void Write(char value)
        {
            Write(value.ToString());
        }

        public override void Write(char[] buffer, int index, int count)
        {
            Write(new string(buffer, index, count));
        }

        public override void WriteLine(string value)
        {
            Write(value + Environment.NewLine);
        }

        public override void Write(string value)
        {
            if (Debugger.IsLogging())
            {
                Debugger.Log(0, null, value);
            }
            else
            {
                // kernel32!OutputDebugString upstream; Trace.Write reaches the same
                // DefaultTraceListener sink without a Windows-only import.
                Trace.Write(value ?? string.Empty);
            }
        }
    }
}
