Imports System.Net
Imports System.Data.SqlClient

Public Class Service1
    Public Sub OnDebug()
        OnStart(Nothing)
    End Sub

    Public firstRun As Boolean = True
    Public help As New Helper

    Public ticker As New Timers.Timer

    Protected Overrides Sub OnStart(ByVal args() As String)

        ticker.Enabled = True

        AddHandler ticker.Elapsed, AddressOf Startkontrol
        ticker.Start()

    End Sub

    Protected Overrides Sub OnStop()
        ticker.Stop()
    End Sub

    Public running As Boolean

    Sub Startkontrol()
        help.config = New Indstillinger(help.HentIndstillinger)
        ticker.Interval = help.config.timer_interval
        If Not running Then
            Dim dt As DataTable = help.HentData("EXEC startkontrol", Nothing)
            Dim korsel_id As Integer = dt.Rows(0)(0)
            Dim korselstype As String = dt.Rows(0)(1).ToString
            Console.WriteLine("Korsel id: " & korsel_id)
            If korsel_id > 0 Then
                Synkroniser(korsel_id, korselstype)
            End If
        End If

    End Sub

    Sub Synkroniser(korsel_id As Integer, korselstype As String)

        running = True
        Dim par As New SqlClient.SqlParameter("@korsel_id", korsel_id)

        Try
            help.WriteLog(korsel_id, "Starter synkronisering af job nummer " & korsel_id.ToString, "")
            firstRun = True
            If LoginIsValid(korsel_id) Then
                help.ExecQuery("UPDATE program_korsler SET korer = 1, [status] = 'Synkroniserer nu...', starttidspunkt = GETDATE() WHERE korsel_id = @korsel_id", par)
                If korselstype = "3" Then
                    DownloadSkoledage(korsel_id)
                ElseIf korselstype = "4" Then
                    DownloadHoldListe(korsel_id, False)
                Else
                    DownloadHoldListe(korsel_id)
                End If
                help.ExecQuery("UPDATE program_korsler SET korer = 0, [status] = 'OK', sluttidspunkt = GETDATE() WHERE korsel_id = @korsel_id", par)
            Else
                help.ExecQuery("UPDATE program_korsler SET [status] = 'Udsat til næste tick' WHERE korsel_id = @korsel_id", par)
            End If
            help.WriteLog(korsel_id, "Slutter synkronisering af job nummer " & korsel_id.ToString, "")
        Catch ex As Exception
            help.ExecQuery("UPDATE program_korsler SET korer = 0, [status] = 'Fejlet - <a href=""/log?korsel=" & korsel_id & """>se log</a>', sluttidspunkt = GETDATE() WHERE korsel_id = @korsel_id", par)
            help.WriteLog(korsel_id, ex.Message, ex.StackTrace)
            Throw ex
        End Try
        running = False

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

        If SynkHold Then
            Dim json As String = help.HentJson(Helper.url.holdliste)
            help.SendJson("EXEC sync_hold @json", json)
        End If

        Dim cnt As Integer = 0
        Dim dt As DataTable = help.HentData("SELECT * FROM program_sync ORDER BY prioritet", Nothing)
        help.WriteLog(korsel_id, "Synkroniserer " & dt.Rows.Count & " hold", "")
        Console.WriteLine("Synkroniserer " & dt.Rows.Count & " hold")
        For Each row As DataRow In dt.Rows
            cnt += 1
            Try

                DownloadHold(row("akti_id"), dt.Rows.Count, cnt)
                DownloadTilstededage(row("akti_id"))

                ' Fjern sync anmodningen
                Dim par As New SqlClient.SqlParameter("@akti_id", SqlDbType.Int)
                par.Value = row("akti_id")
                help.ExecQuery("EXEC sync_ok @akti_id", par)

            Catch ex As Exception
                help.WriteLog(korsel_id, "Fejlet hold: " & row("akti_id") & ". " & ex.Message, ex.StackTrace)
            End Try

            Console.WriteLine("Sov " & help.config.sleep)
            Threading.Thread.Sleep(help.config.sleep)
        Next

        help.ExecQuery("EXEC sync_housekeeping @korsel_id", New SqlClient.SqlParameter("@korsel_id", korsel_id))

    End Sub

    Sub DownloadHold(akti_id As Integer, antal As Integer, cnt As Integer)

        Console.WriteLine("Downloader hold " & akti_id & "(" & cnt & " / " & antal & ")")

        Dim json As String = help.HentJson(Helper.url.hold, akti_id)

        ' Sync maps
        If firstRun Then help.SendJson("EXEC sync_maps @json", json) : firstRun = False

        help.SendJson("EXEC sync_detaljer @akti_id, @json", akti_id, json)

    End Sub

    Sub DownloadTilstededage(akti_id As Integer)
        help.SendJson("EXEC sync_tilstededage @akti_id, @json", akti_id, help.HentJson(Helper.url.tilstededage, akti_id))
    End Sub

    Function LoginIsValid(korsel_id As Integer) As Boolean

        Dim req As HttpWebRequest = DirectCast(HttpWebRequest.Create(help.config.url_test_login), HttpWebRequest)
        req.Headers.Add("cookie", help.config.jsession)

        Dim res As HttpWebResponse = DirectCast(req.GetResponse(), HttpWebResponse)

        If InStr(res.Headers.Get("Set-Cookie"), "JSESSIONIDSSO=REMOVE") > 0 Then
            help.WriteLog(korsel_id, "Session cookie er udløbet", "")

            Dim cmd As String = "powershell -executionpolicy bypass ""C:\Uplus\login.ps1"""

            Dim psi As New ProcessStartInfo("cmd.exe", cmd)
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            'psi.WorkingDirectory = Application.StartupPath
            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            psi.RedirectStandardOutput = True
            psi.Verb = "runas"

            Dim p As Process
            p = Process.Start(psi)
            p.BeginOutputReadLine()
            While Not p.StandardOutput.EndOfStream
                Console.WriteLine(p.StandardOutput.ReadLine)
            End While
            p.WaitForExit()

            ' Vent 10 sekunder og prøv igen
            help.WriteLog(korsel_id, "Session cookie er hentet. Udsætter start med 10 sekunder", "")
            ticker.Interval = 10000

            Return False
        Else
            Return True
        End If

    End Function

End Class
