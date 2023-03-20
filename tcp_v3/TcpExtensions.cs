using System;
using System.Net.Sockets;
using System.Threading;

/// Extensions to TcpListener
public static class TcpExtensions
{

    /// Metoda opakowująca asynchroniczne wywołanie AcceptSocket w synchroniczne zawołanie przez ManualResetEvent.
    /// Dzięki temu czekając na połączenie można zastosować Abort(); 
    public static Socket AcceptSocketAbortable(this TcpListener tcpListener)
    {
        Socket socket = null;
        Exception ex = null;
        var clientConnected = new ManualResetEvent(false);
        clientConnected.Reset();
        tcpListener.BeginAcceptSocket((delegate (IAsyncResult asyncResult)
        {
            try
            {
                socket = tcpListener.EndAcceptSocket(asyncResult);
            }
            catch (Exception _ex)
            {
                ex = _ex;
            }
            clientConnected.Set();
        }), null);
        clientConnected.WaitOne();
        if (ex != null) throw ex;
        return socket;
    }

    public static int ReceiveAbortable(this Socket s, byte[] buf, int size, SocketFlags socketFlags)
    {
        int ret = 0;
        Exception ex = null;
        var clientReceived = new ManualResetEvent(false);
        clientReceived.Reset();
        s.BeginReceive(buf, 0, size, socketFlags, (delegate (IAsyncResult asyncResult)
        {
            try
            {
                ret = s.EndReceive(asyncResult);
            }
            catch (Exception _ex)
            {
                ex = _ex;
            }
            clientReceived.Set();
        }), null);
        clientReceived.WaitOne();
        if (ex != null) throw ex;
        return ret;
    }

    public static TcpClient AcceptTcpClientAbortable(this TcpListener tcpListener)
    {
        TcpClient client = null;
        Exception ex = null;
        var clientConnected = new ManualResetEvent(false);
        clientConnected.Reset();
        tcpListener.BeginAcceptTcpClient((delegate (IAsyncResult asyncResult)
        {
            try
            {
                client = tcpListener.EndAcceptTcpClient(asyncResult);
            }
            catch (Exception _ex)
            {
                ex = _ex;
            }
            clientConnected.Set();
        }), null);
        clientConnected.WaitOne();

        if (ex != null) throw ex;
        return client;
    }
}