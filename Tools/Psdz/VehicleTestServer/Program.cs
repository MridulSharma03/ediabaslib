﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EdiabasLib;
using HttpMultipartParser;
using ISimpleHttpListener.Rx.Enum;
using ISimpleHttpListener.Rx.Model;
using SimpleHttpListener.Rx.Extension;
using SimpleHttpListener.Rx.Model;
using SimpleHttpListener.Rx.Service;

namespace VehicleTestServer
{
    class Program
    {
        static int Main(string[] args)
        {
            TextWriter outWriter = Console.Out;
            EdiabasNet ediabas = EdiabasSetup();
            EdWebServer ediabasWebServer = new EdWebServer(ediabas, Console.Out);
            ediabasWebServer.StartTcpListener("http://127.0.0.1:8080");
            outWriter?.WriteLine("Press ESC to stop");
            do
            {
                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(100);
                }
            }
            while (Console.ReadKey(true).Key != ConsoleKey.Escape);

            ediabasWebServer.Dispose();
            return 0;
        }

        private static EdiabasNet EdiabasSetup()
        {
            EdInterfaceEnet edInterfaceEnet = new EdInterfaceEnet(false);
            EdiabasNet ediabas = new EdiabasNet
            {
                EdInterfaceClass = edInterfaceEnet,
            };
            edInterfaceEnet.RemoteHost = "127.0.0.1";
            edInterfaceEnet.IcomAllocate = false;

            return ediabas;
        }
    }
}