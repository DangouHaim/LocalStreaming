﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Xamarin.Forms;

namespace LocalStreamingApp
{
    public partial class MainPage : ContentPage
    {
        private const string AddressApiUrl = "https://bsite.net/dangou/api/address";

        private bool _remote = false;

        private RelativeLayout _layout = new RelativeLayout();

        private int _framesOnScreen = 4;

        private static ImageSource _screenImage { get; set; }
        public static ImageSource ScreenImage
        {
            get => _screenImage;
            set
            {
                if(value == _screenImage)
                {
                    return;
                }

                _screenImage = value;
            }
        }

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
            Connect();
        }

        private void Connect()
        {
            Content = _layout;

            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        string ipAddress = GetRemoteAddress();

                        var client = new TcpClient(ipAddress, 5858);
                        var clientStream = client.GetStream();
                        IFormatter formatter = new BinaryFormatter();
                        byte[] data;

                        while (true)
                        {
                            data = (byte[])formatter.Deserialize(clientStream);
                            ScreenImage = ImageSource.FromStream(() => new MemoryStream(data));
                        }
                    }
                    catch { }
                }
            });

            int framesCount = 0;
            Device.StartTimer(TimeSpan.FromMilliseconds(25), () =>
            {
                try
                {
                    framesCount++;
                    if (framesCount % 10 == 0)
                    {
                        GC.Collect();
                    }
                    UpdateScreen();
                }
                catch { }

                return true;
            });
        }

        private void UpdateScreen()
        {
            _layout.Children.Add(Screen(),
                Constraint.Constant(0),
                Constraint.Constant(0),
                Constraint.RelativeToParent((parent) => { return parent.Width; }),
                Constraint.RelativeToParent((parent) => { return parent.Height; }));

            if (_layout.Children.Count > _framesOnScreen)
            {
                _layout.Children.RemoveAt(0);
            }

            _layout.GestureRecognizers.Add(new TapGestureRecognizer()
            {
                NumberOfTapsRequired = 1,
                Command = new Command(() =>
                {
                    _framesOnScreen++;
                    if (_framesOnScreen > 10)
                    {
                        _framesOnScreen = 1;
                    }
                })
            });
        }

        public static Image Screen()
        {
            Image ini = new Image()
            {
                Source = ScreenImage
            };
            return ini;
        }

        private string GetRemoteAddress()
        {
            WebRequest webRequest = WebRequest.Create(AddressApiUrl);
            using(var sr = new StreamReader(webRequest.GetResponse().GetResponseStream()))
            {
                return sr.ReadToEnd().Replace("\"", "");
            }
        }
    }
}
