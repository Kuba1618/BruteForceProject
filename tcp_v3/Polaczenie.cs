using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace tcp_v3
{

    [Serializable]
    public struct Komunikat
    {
        public string tresc;
        public bool wazna;
        public string nadawca;
        public DateTime czasNadania;
        public DateTime czasOdbioru;
    }


    class KomunikatEventArgs : EventArgs
    {
        public Komunikat kom;
        public long idPolaczenia;
        public KomunikatEventArgs()
        {
            kom.czasNadania = DateTime.Now;
            kom.nadawca = "";
            kom.tresc = "";
            kom.wazna = true;
        }
    }

    class Klient
    {
        public Thread watek;
        public TcpClient tcpKlient;
    }

    class PolaczenieZerwaneEventArgs : EventArgs
    {
        public long idPolaczenia;
        public PolaczenieZerwaneEventArgs(long id_pol)
        {
            idPolaczenia = id_pol;
        }
    }


    class PolaczenieUstanowioneEventArgs : EventArgs
    {
        public long idPolaczenia;
        public string adres;
        public PolaczenieUstanowioneEventArgs(long id_pol, string _adres)
        {
            idPolaczenia = id_pol;
            adres = _adres;
        }
    }

    class Polaczenie
    {
        public delegate void KomunikatEventsHandler(object sender, KomunikatEventArgs e);
        public event KomunikatEventsHandler KomunikatPrzybyl; //event wysyłany w razie nadejścia komunikatu

        public delegate void PolaczenieZerwaneEventsHandler(object sender, PolaczenieZerwaneEventArgs e);
        public event PolaczenieZerwaneEventsHandler PolaczenieZerwane; //event wysyłany w razie zerwania polaczenia

        public delegate void PolaczenieUstanowioneEventsHandler(object sender, PolaczenieUstanowioneEventArgs e);
        public event PolaczenieUstanowioneEventsHandler PolaczenieUstanowione; //event wysyłany w razie ustanowienia polaczenia

        private int MaxConnected = 400; //maksymalna liczba połączeń
        private static long connectId = 1; //id połaczenia 

        private TcpListener tcpLsn;
        private Thread watekSerwera;

        //lista trzymająca wątki i tcp klientów
        private Hashtable listaKlientow = new Hashtable();

        private BinaryFormatter bf = new BinaryFormatter();

        /// Wystartuj serwer na podanym porcie i adresie
        public bool startSerwer(string adres, int port)
        {

            tcpLsn = new TcpListener(IPAddress.Parse(adres), port);
            tcpLsn.Start();

            //Console.WriteLine("Słucham na: " + tcpLsn.LocalEndpoint.ToString()); //wypisanie na czym jest nasłuch

            watekSerwera = new Thread(new ThreadStart(watekCzekajNaKlientow)); //przygotowanie wątku czekającego na połączenia
            watekSerwera.Name = "wątek serwera czekający na klientów id: " + 0;
            watekSerwera.IsBackground = false;
            watekSerwera.Start(); //wystartowanie wątku czekającego na połączenia
            return true;
        }



        /// Połącz się z klientem do zdalnego serwera na podanym porcie
        public bool startKlient(string adres, int port)
        {
            Klient kli = new Klient();
            IPAddress hostadd = IPAddress.Parse(adres);
            IPEndPoint EPhost = new IPEndPoint(hostadd, port);
            kli.tcpKlient = new TcpClient();
            try
            {
                kli.tcpKlient.Connect(EPhost);
                if (kli.tcpKlient.Client.Connected)
                {
                    kli.watek = new Thread(new ParameterizedThreadStart(watekCzytajZSocketa));
                    kli.watek.Name = "Wątek kllienta czytający z socketa id: 0";
                    listaKlientow.Add(0L, kli);
                    kli.watek.Start(0L);
                }
                else return false;
            }
            catch (Exception e1)
            {
                Console.WriteLine("Jakiś błąd: " + e1.Message);
                return false;
            }
            return true;
        }

        /// Wysyła komunikat do wszystkich z którymi mamy połączenie
        public bool wyslij(Komunikat kom)
        {
            //wyślij wszystkim połączonym (z indeksem 0 jest połączenie klienta z serwerem)
            foreach (Klient kli in listaKlientow.Values) 
            {
                if (kli.tcpKlient.Connected)
                {
                    kom.czasNadania = DateTime.Now;
                    bf.Serialize(kli.tcpKlient.GetStream(), kom);
                }
            }
            return true;
        }

        public void odlacz()
        {
            lock (listaKlientow)
            {
                //zamknij wszystkie połaczanie
                foreach (Klient kli in listaKlientow.Values)
                {
                    kli.tcpKlient.Client.Disconnect(false);
                    kli.tcpKlient.Close();
                    kli.watek.Abort();
                }
                
                listaKlientow.Clear();
                
                //w przypadku serwera usuń wątek serwera i zamknij połączenie
                if (watekSerwera != null)
                    watekSerwera.Abort();
                if (tcpLsn != null) 
                    tcpLsn.Server.Close();
            }
        }

        /// Wątek czekający na klientów
        public void watekCzekajNaKlientow()
        {
            try
            {
                while (true)
                {
                    Klient kli = new Klient();
                   
                    // Accept blokuje dopóki nie pojawi się jakiś klient                      
                    kli.tcpKlient = tcpLsn.AcceptTcpClientAbortable();
                    
                    //kli.tcpKlient = tcpLsn.AcceptTcpClient(); //ta wersja nie reaguje na abort i interupt
                    lock (listaKlientow)
                    {
                        if (connectId < long.MaxValue - 1) //jak już zapełnimy identyfikatory to zaczynamy od 1
                            Interlocked.Increment(ref connectId);
                        else
                            connectId = 1;
                        Console.WriteLine("połączono z: " + kli.tcpKlient.Client.RemoteEndPoint.ToString());

                        if (listaKlientow.Count < MaxConnected)
                        {
                            //znajdź pierwsze wolne connectId
                            while (listaKlientow.Contains(connectId)) 
                            {
                                Interlocked.Increment(ref connectId);
                            }
                            
                            //przygotuj wątek czytający
                            kli.watek = new Thread(new ParameterizedThreadStart(watekCzytajZSocketa));
                            kli.watek.Name = "wątek czytający z socketa id: " + connectId.ToString();
                            
                            //zapamiętaj nowo podłączonego klienta
                            listaKlientow.Add(connectId, kli);
                            
                            //wystartuj wątek czytający
                            kli.watek.Start(connectId);
                            PolaczenieUstanowioneEventArgs arg = new PolaczenieUstanowioneEventArgs(connectId, kli.tcpKlient.Client.RemoteEndPoint.ToString());
                            if (PolaczenieUstanowione != null)
                                PolaczenieUstanowione(this, arg);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wątek czekający na klientów dostał wyjątkiem: " + ex.Message);
            }
        }


        public void watekCzytajZSocketa(object id)
        {
            long realId = (long)id;
            TcpClient tcpclient = ((Klient)listaKlientow[realId]).tcpKlient;
            while (true)
            {
                if (tcpclient.Connected)
                {
                    try
                    {
                        Komunikat odebranyKom = (Komunikat)bf.Deserialize(tcpclient.GetStream());
                        KomunikatEventArgs arg = new KomunikatEventArgs();
                        arg.kom = odebranyKom;
                        arg.kom.czasOdbioru = DateTime.Now;
                        arg.idPolaczenia = realId;
                        if (KomunikatPrzybyl != null)
                        {
                            KomunikatPrzybyl(this, arg);
                        }
                    }
                    catch (SerializationException )
                    {
                        break;
                    }
                    catch (Exception )
                    {
                        if (!tcpclient.Connected)
                        {
                            break;
                        }
                    }
                }
            }
            lock (listaKlientow)
            {
                listaKlientow.Remove(realId);
            }
            PolaczenieZerwaneEventArgs arg2 = new PolaczenieZerwaneEventArgs(realId);
            if (PolaczenieZerwane != null)
            {
                PolaczenieZerwane(this, arg2); //zasygnalizuj zerwane połączenie
            }
        }

    }
}

