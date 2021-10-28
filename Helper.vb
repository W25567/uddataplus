Imports System.Text
Imports System.Net

Public Class Helper

    Public Enum url
        login = 1
        holdliste = 2
        hold = 3
        tilstededage = 4
        test_login = 5
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
        End Select
        Console.WriteLine(adresse)
        Dim wc As New WebClient
        wc.Encoding = Encoding.UTF8
        wc.Headers.Add("cookie", config.jsession)
        Dim s As String = wc.DownloadString(adresse)
        Console.WriteLine(config.jsession)
        'Console.WriteLine(s)
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

    Public Sub ExecQuery(command As String, ParamArray params() As SqlClient.SqlParameter)

        Dim cnn As New SqlClient.SqlConnection
        cnn.ConnectionString = cnnString
        Using cmd As New SqlClient.SqlCommand(command, cnn)
            cmd.CommandTimeout = 3600
            For Each par As SqlClient.SqlParameter In params
                cmd.Parameters.Add(par)
            Next
            cmd.Connection.Open()
            cmd.ExecuteNonQuery()
            cmd.Parameters.Clear()
        End Using

    End Sub

End Class

