Namespace Provider





    Friend NotInheritable Class EntityGateToken







        Friend Property ValidityDate As DateTime







        Friend Property IsManaged As Boolean = True







        Friend Property IsTracked As Boolean = True




        Friend Sub New()
            ValidityDate = DateTime.UtcNow.AddMinutes(1)
        End Sub

    End Class

End Namespace
