Imports System.Text
Imports System.Net
Imports ICSharpCode.SharpZipLib
Imports System.IO

Public Class Helper

    Public Enum url
        login = 1
        holdliste = 2
        hold = 3
        tilstededage = 4
        test_login = 5
        skoledage = 6
    End Enum

    Public cnnString = "Data Source=AMUS-APP\AMUS_KVICHSYS;Initial Catalog=uddataplus;Integrated Security=True"
    Public config As New Indstillinger(HentIndstillinger)

    Public Function HentIndstillinger() As DataTable
        Return HentData("SELECT * FROM program_indstillinger", Nothing)
    End Function

    Public Function HentData(kommando As String, ParamArray params() As SqlClient.SqlParameter) As DataTable
        Dim cnn As New SqlClient.SqlConnection
        cnn.ConnectionString = cnnString
        Dim cmd As New SqlClient.SqlCommand(kommando, cnn)
        If Not params Is Nothing Then
            For Each par As SqlClient.SqlParameter In params
                cmd.Parameters.Add(par)
            Next
        End If
        Dim adp As New SqlClient.SqlDataAdapter(cmd)
        Dim dt As New DataTable
        adp.Fill(dt)
        Return dt
    End Function

    Public Function WebKlient(Optional login As Boolean = False) As WebClient
        Dim wc As New WebClient
        wc.Encoding = Encoding.UTF8
        If login Then
            wc.Headers.Add("Host", "all.uddataplus.dk")
            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:94.0) Gecko/20100101 Firefox/94.0")
            wc.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8")
            wc.Headers.Add("Accept-Language", "da,en-US;q=0.7,en;q=0.3")
            wc.Headers.Add("Accept-Encoding", "gzip, deflate, br")
            wc.Headers.Add("Content-Type", "application/x-www-form-urlencoded")
            'wc.Headers.Add("Content-Length", "72")
            'wc.Headers.
            wc.Headers.Add("Origin", "https://all.uddataplus.dk")
            'wc.Headers.Add("Connection", "keep-alive")
            wc.Headers.Add("Referer", "https://all.uddataplus.dk/")
            wc.Headers.Add("Cookie", "usession=02977985D6C374125D324C73F387695B.default; instkey=64233665; instnr=621407; tab=direkte; cok=1; fguOverblikFilter32610={""ownFilter"":""alle"",""includeFormer"":false,""searchString"":""""}; usession=BE123D4BB5B8A6D319D3B1AB3C691913.default")
            wc.Headers.Add("Upgrade-Insecure-Requests", "1")
            wc.Headers.Add("Sec-Fetch-Dest", "document")
            wc.Headers.Add("Sec-Fetch-Mode", "navigate")
            wc.Headers.Add("Sec-Fetch-Site", "same-origin")
            wc.Headers.Add("Sec-Fetch-User", "?1")
            wc.Headers.Add("TE", "trailers")

        Else
            wc.Headers.Add("cookie", config.jsession)
            wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:94.0) Gecko/20100101 Firefox/94.0")
            wc.Headers.Add("Host", "all.uddataplus.dk")
            wc.Headers.Add("Accept", "application/json, text/plain, */*")
            wc.Headers.Add("Accept-Language", "da, en - US;q=0.7, en;q=0.3")
            wc.Headers.Add("Accept-Encoding", "gzip, deflate, br")
            wc.Headers.Add("Referer", "https://all.uddataplus.dk/react/uddadm/amukurser/")
            wc.Headers.Add("Sec-Fetch-Dest", "Empty")
            wc.Headers.Add("Sec-Fetch-Mode", "cors")
            wc.Headers.Add("Sec-Fetch-Site", "same-origin")
        End If
        Return wc
    End Function

    Public Function HentJson(url As Helper.url, Optional akti_id As String = "") As String

        Dim adresse As String = ""
        Select Case url
            Case url.login : adresse = config.url_login
            Case url.holdliste
                Dim start As Date = DateAdd(DateInterval.Day, (config.start_antal_dage * -1), Now)
                Dim slut As Date = DateAdd(DateInterval.Month, config.slut_antal_mdr, start)
                adresse = config.url_holdliste.Replace("[start]", FormatDato(start)).Replace("[slut]", FormatDato(slut))
            Case url.hold : adresse = config.url_hold.Replace("[akti_id]", akti_id)
            Case url.tilstededage : adresse = config.url_tilstededage.Replace("[akti_id]", akti_id)
            Case url.test_login : adresse = config.url_test_login
            Case url.skoledage : adresse = config.url_skoledage.Replace("[skka_id]", akti_id)
        End Select
        Console.WriteLine(adresse)

        Dim wc As WebClient = WebKlient()

        Console.WriteLine(config.jsession)
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

        Dim b() As Byte = wc.DownloadData(adresse)
        Dim s As String
        Try
            Dim intChunkSize As Integer = 4096
            Dim gz As New GZip.GZipInputStream(New MemoryStream(b))
            Dim intSizeRead As Integer
            Dim unzipBytes(intChunkSize) As Byte
            Dim OutputStream As New MemoryStream

            While True
                intSizeRead = gz.Read(unzipBytes, 0, intChunkSize)
                If intSizeRead > 0 Then
                    OutputStream.Write(unzipBytes, 0, intSizeRead)
                Else
                    Exit While
                End If
            End While

            s = System.Text.Encoding.UTF8.GetString(OutputStream.ToArray)
        Catch ex As Exception
            s = Encoding.Default.GetString(b)
            Console.WriteLine("Bruge råt format")
        End Try

        Return s
    End Function

    Private Function FormatDato(dato As Date) As String
        Return dato.Day.ToString("00") & "." & dato.Month.ToString("00") & "." & dato.Year.ToString("0000")
    End Function

    Public Sub SendJson(kommando As String, json As String)
        Dim par As New SqlClient.SqlParameter("@json", json)
        ExecQuery(kommando, par)
    End Sub

    Public Sub SendJson(kommando As String, akti_id As String, json As String)
        Dim par1 As New SqlClient.SqlParameter("@json", json)
        Dim par2 As New SqlClient.SqlParameter("@akti_id", akti_id)
        ExecQuery(kommando, par1, par2)
    End Sub

    Public Sub WriteLog(korsel_id As Integer, logtekst As String, stacktrace As String)
        Console.WriteLine("LOG: " & logtekst)
        Dim par0 As New SqlClient.SqlParameter("@korsel_id", korsel_id)
        Dim par1 As New SqlClient.SqlParameter("@logtekst", logtekst)
        Dim par2 As New SqlClient.SqlParameter("@stacktrace", stacktrace)
        Dim sql As String = "INSERT INTO program_log (korsel_id, log_tekst, stacktrace) VALUES (@korsel_id, @logtekst, @stacktrace)"
        ExecQuery(sql, par0, par1, par2)
    End Sub

    Public Sub WriteStatus(status As String)
        Console.WriteLine("STATUS: " & status)
        Dim par0 As New SqlClient.SqlParameter("@STATUS", status)
        Dim sql As String = "EXEC update_robotstatus @STATUS"
        ExecQuery(sql, par0)
    End Sub


    Public Sub ExecQuery(command As String, ParamArray params() As SqlClient.SqlParameter)

        Dim cnn As New SqlClient.SqlConnection
        cnn.ConnectionString = cnnString
        Using cmd As New SqlClient.SqlCommand(command, cnn)
            cmd.CommandTimeout = 3600
            If Not params Is Nothing Then
                For Each par As SqlClient.SqlParameter In params
                    cmd.Parameters.Add(par)
                Next
            End If
            cmd.Connection.Open()
            cmd.ExecuteNonQuery()
            cmd.Parameters.Clear()
        End Using

    End Sub

End Class

