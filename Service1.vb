Imports System.Net
Imports System.Data.SqlClient
Imports System.IO
Imports ICSharpCode.SharpZipLib
Imports System.Text

Public Class Service1
    Public Sub OnDebug()
        OnStart(Nothing)
    End Sub

    Public firstRun As Boolean = True
    Public help As New Helper

    Public ticker As New Timers.Timer

    Sub udpak()
        Dim ms As New MemoryStream
        Using fs As FileStream = New FileStream("C:\Uplus\data\hold15812", FileMode.Open, FileAccess.Read)
            fs.CopyTo(ms)
        End Using

        Dim s As String
        Try
            Dim intChunkSize As Integer = 4096
            Dim gz As New GZip.GZipInputStream(New MemoryStream(ms.ToArray))
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
            s = Encoding.Default.GetString(ms.ToArray)
            Console.WriteLine("Bruge råt format")
        End Try

        ms.Close()

        File.WriteAllText("C:\Uplus\hold15812.json", s)

    End Sub


    Protected Overrides Sub OnStart(ByVal args() As String)

        help.WriteStatus("Starter service...")

        ticker.Enabled = True

        AddHandler ticker.Elapsed, AddressOf Startkontrol

        ticker.Start()
        'udpak()
    End Sub

    Protected Overrides Sub OnStop()
        help.WriteStatus("Stopper service...")
        ticker.Stop()
    End Sub

    Public running As Boolean

    Sub Startkontrol()

        If Not running Then
            help.WriteStatus("Startkontrol kører...")

            ' Frisk indstilinger op
            help.config = New Indstillinger(help.HentIndstillinger)
            ticker.Interval = help.config.timer_interval

            ' Kør startkontrol
            Dim dt As DataTable = help.HentData("EXEC startkontrol", Nothing)
            Dim korsel_id As Integer = dt.Rows(0)(0)
            Dim korselstype As String = dt.Rows(0)(1).ToString

            If korsel_id > 0 Then
                Console.WriteLine("Korsel id: " & korsel_id)
                help = New Helper(korsel_id)
                Synkroniser(korsel_id, korselstype)
            Else
                Console.WriteLine("Der var ingen aktuelle kørsler")
                Console.WriteLine("Afventer næste kørsel")
            End If
        End If

    End Sub

    Sub Synkroniser(korsel_id As Integer, korselstype As String)

        running = True
        help.WriteStatus("Synkroniserer nu")

        Try

            help.WriteLog(korsel_id, "Starter synkronisering af job nummer " & korsel_id.ToString, "")

            firstRun = True

            If LoginIsValid(korsel_id) Then
                ' Start synkronisering
                Dim sql As String = "UPDATE program_korsler SET korer = 1, [status] = 'Synkroniserer nu...', starttidspunkt = GETDATE() WHERE korsel_id = @korsel_id"
                help.ExecQuery(sql, New SqlClient.SqlParameter("@korsel_id", korsel_id))

                If korselstype = "3" Then
                    DownloadSkoledage(korsel_id)
                ElseIf korselstype = "4" Then
                    DownloadHoldListe(korsel_id, False)
                Else
                    DownloadHoldListe(korsel_id)
                End If

                ' Alt er gået godt
                sql = "UPDATE program_korsler SET korer = 0, [status] = 'OK', sluttidspunkt = GETDATE() WHERE korsel_id = @korsel_id"
                help.ExecQuery(sql, New SqlClient.SqlParameter("@korsel_id", korsel_id))
            Else
                ' Skriv at vi venter
                Dim sql As String = "UPDATE program_korsler SET [status] = 'Udsat til næste tick' WHERE korsel_id = @korsel_id"
                help.ExecQuery(sql, New SqlClient.SqlParameter("@korsel_id", korsel_id))
            End If

            help.WriteLog(korsel_id, "Slutter synkronisering af job nummer " & korsel_id.ToString, "")

        Catch ex As Exception
            Dim sql As String = "UPDATE program_korsler SET korer = 0, [status] = 'Fejlet - <a href=""/log?korsel=" & korsel_id & """>se log</a>', sluttidspunkt = GETDATE() WHERE korsel_id = @korsel_id"
            help.ExecQuery(sql, New SqlClient.SqlParameter("@korsel_id", korsel_id))
            help.WriteLog(korsel_id, ex.Message, ex.StackTrace)
        End Try
        running = False
        help.WriteStatus("Afventer næste kørsel")

    End Sub

    Sub DownloadSkoledage(korsel_id As Integer)

        Dim skoledagskalendere As DataTable = help.HentData("SELECT * FROM skoledagskalendere", Nothing)
        help.ExecQuery("EXEC create_skoledag_sequence", Nothing)

        For Each row As DataRow In skoledagskalendere.Rows

            Console.WriteLine("Synkroniserer skoledagskalender '" & row("skoledagskalender").ToString & "' med skka_id = " & row("skka_id"))

            Dim p1 As New SqlParameter("@SKKA_ID", row("skka_id"))
            Dim p2 As New SqlParameter("@JSON", help.HentJson(Helper.url.skoledage, row("skka_id")))

            help.ExecQuery("EXEC sync_skoledage @SKKA_ID, @JSON", p1, p2)

            Console.WriteLine("Sov " & help.config.sleep)
            Threading.Thread.Sleep(help.config.sleep)

        Next

    End Sub
    Sub DownloadHoldListe(korsel_id As Integer, Optional SynkHold As Boolean = True)

        ' Vi henter holdlisten lige meget hvad, men synker kun, hvis der er bedt om det
        Dim holdliste As String = help.HentFil(Helper.url.holdliste)
        If SynkHold Then help.SendJson("EXEC sync_hold @json", holdliste)

        ' Hent hold i sync-tabellen
        Dim dt As DataTable = help.HentData("SELECT * FROM program_sync ORDER BY prioritet", Nothing)
        help.WriteLog(korsel_id, "Synkroniserer " & dt.Rows.Count & " hold", "")

        ' Løb igennem alle rækker og synkroniser
        Dim cnt As Integer = 0 ' Tæller antallet af rækker vi er løbet igennem
        Dim chk As Integer = 0 ' Holder styr på, at vi tjekker om jobbet er annulleret
        For Each row As DataRow In dt.Rows
            cnt += 1
            chk += 1

            If DownloadHold(korsel_id, row("akti_id"), dt.Rows.Count, cnt) And DownloadTilstededage(korsel_id, row("akti_id")) Then
                Dim p As New SqlClient.SqlParameter("@akti_id", SqlDbType.Int) With {
                    .Value = row("akti_id")
                }
                help.ExecQuery("EXEC sync_ok @akti_id", p)
            End If

            If chk > 10 Then
                If help.HentData("EXEC get_korselsstatus @KORSEL_ID", New SqlParameter("@KORSEL_ID", korsel_id)).Rows(0)(0) = 1 Then
                    chk = 0
                Else
                    help.WriteLog(korsel_id, "Stopper synkroniseringen, da jobbet er annulleret udefra", cnt & " af " & dt.Rows.Count & " hold blev synkroniseret")
                    Exit For
                End If
            End If

            System.Threading.Thread.Sleep(help.config.sleep)

        Next

        ' TODO: Extend housekeeping and try statements with log error and follow up
        help.ExecQuery("EXEC sync_housekeeping @korsel_id", New SqlClient.SqlParameter("@korsel_id", korsel_id))

    End Sub

    Sub DownloadHoldListe2(korsel_id As Integer, Optional SynkHold As Boolean = True)

        If SynkHold Then
            Dim json As String = help.HentJson(Helper.url.holdliste)
            help.SendJson("EXEC sync_hold @json", json)
        End If

        Dim cnt As Integer = 0
        Dim tjekcnt As Integer = 0
        Dim dt As DataTable = help.HentData("SELECT * FROM program_sync ORDER BY prioritet", Nothing)
        help.WriteLog(korsel_id, "Synkroniserer " & dt.Rows.Count & " hold", "")
        Console.WriteLine("Synkroniserer " & dt.Rows.Count & " hold")
        For Each row As DataRow In dt.Rows
            cnt += 1
            tjekcnt += 1
            Try

                'DownloadHold(row("akti_id"), dt.Rows.Count, cnt)
                'DownloadTilstededage(row("akti_id"))

                ' Fjern sync anmodningen
                Dim par As New SqlClient.SqlParameter("@akti_id", SqlDbType.Int)
                par.Value = row("akti_id")
                help.ExecQuery("EXEC sync_ok @akti_id", par)

            Catch ex As Exception
                help.WriteLog(korsel_id, "Fejlet hold: " & row("akti_id") & ". " & ex.Message, ex.StackTrace)
            End Try

            Console.WriteLine("Sov " & help.config.sleep)
            Threading.Thread.Sleep(help.config.sleep)

            If tjekcnt > 10 Then
                If help.HentData("EXEC get_korselsstatus @KORSEL_ID", New SqlParameter("@KORSEL_ID", korsel_id)).Rows(0)(0) = 1 Then
                    tjekcnt = 0
                Else
                    help.WriteLog(korsel_id, "Stopper synkroniseringen, da jobbet er annulleret udefra", cnt & " af " & dt.Rows.Count & " hold blev synkroniseret")
                    Exit For
                End If
            End If
        Next

        help.ExecQuery("EXEC sync_housekeeping @korsel_id", New SqlClient.SqlParameter("@korsel_id", korsel_id))

    End Sub

    Function DownloadHold(korsel_id As Integer, akti_id As Integer, antal As Integer, cnt As Integer) As Boolean

        Console.WriteLine("Downloader hold " & akti_id & " (" & cnt & " / " & antal & ")")

        Try
            ' Hent json
            Dim json As String = help.HentFil(Helper.url.hold, akti_id)

            ' Sync maps
            If firstRun Then help.SendJson("EXEC sync_maps @json", json) : firstRun = False

            ' Send detaltjer
            help.SendJson("EXEC sync_detaljer @akti_id, @json", akti_id, json)

            Return True
        Catch ex As Exception
            help.WriteLog(korsel_id, "Hold: " & akti_id & " - " & ex.Message, If((ex.StackTrace = ""), ex.Message, ex.StackTrace))
            Return False
        End Try

    End Function

    Function DownloadTilstededage(korsel_id As Integer, akti_id As Integer) As Boolean
        Try
            help.SendJson("EXEC sync_tilstededage2 @akti_id, @json", akti_id, help.HentFil(Helper.url.tilstededage, akti_id))
            Return True
        Catch ex As Exception
            help.WriteLog(korsel_id, "Hold: " & akti_id & " - " & ex.Message, If((ex.StackTrace = ""), ex.Message, ex.StackTrace))
            Return False
        End Try
    End Function

    Function LoginIsValid(korsel_id As Integer) As Boolean

        Console.WriteLine("Tjekker om session cookie er gyldig")
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

        Dim req As HttpWebRequest = DirectCast(HttpWebRequest.Create(help.config.url_test_login), HttpWebRequest)
        req.Headers.Add("cookie", help.config.jsession)

        Dim res As HttpWebResponse = DirectCast(req.GetResponse(), HttpWebResponse)

        If InStr(res.Headers.Get("Set-Cookie"), "JSESSIONIDSSO=REMOVE") > 0 Then
            help.WriteLog(korsel_id, "Session cookie er udløbet", "")

            Process.Start("powershell", "-executionpolicy bypass -File ""C:\Uplus\login.ps1""")

            ' Vent 10 sekunder og prøv igen
            help.WriteLog(korsel_id, "Session cookie er hentet. Udsætter start med 30 sekunder", "")
            ticker.Interval = 30000

            Return False
        Else
            help.WriteLog(korsel_id, "Session cookie er gyldig", "")
            Return True
        End If

    End Function


End Class
