
Public Class Indstillinger
    Public jsession_file As String
    Public jsession As String
    Public login_file As String
    Public cnnString As String
    Public url_login As String
    Public url_holdliste As String
    Public url_hold As String
    Public url_tilstededage As String
    Public url_test_login As String
    Public sleep As Integer
    Public start_antal_dage As Integer
    Public slut_antal_mdr As Integer
    Public timer_interval As Integer

    Public Sub New(dt As DataTable)

        'jsession = "JSESSIONIDSSO=3A12773F1A01751E3FD1C6F14A9616A1"

        jsession_file = dt.Rows(0)("jsession_file")
        login_file = dt.Rows(0)("login_file")
        url_login = dt.Rows(0)("url_login")
        url_holdliste = dt.Rows(0)("url_holdliste")
        url_hold = dt.Rows(0)("url_hold")
        url_tilstededage = dt.Rows(0)("url_tilstededage")
        url_test_login = dt.Rows(0)("url_test_login")
        sleep = dt.Rows(0)("sleep")
        start_antal_dage = dt.Rows(0)("start_antal_dage")
        slut_antal_mdr = dt.Rows(0)("slut_antal_mdr")
        timer_interval = dt.Rows(0)("timer_interval")

        Using sr As System.IO.StreamReader = My.Computer.FileSystem.OpenTextFileReader(jsession_file)
            jsession = sr.ReadLine()
        End Using

    End Sub
End Class
