namespace PapayApp.UI.Services;

public static class UserProblem
{
    public static string Describe(Exception error, string action)
    {
        var fix = error switch
        {
            _ when error.Message.Contains("no cabe", StringComparison.OrdinalIgnoreCase) || error.Message.Contains("demasiado", StringComparison.OrdinalIgnoreCase) => "Abre Administrar plantillas, amplía el rectángulo del componente indicado o reduce el texto y guarda la plantilla. Después vuelve a generar el certificado.",
            UnauthorizedAccessException => "Elige una carpeta donde tengas permiso de escritura y comprueba que el archivo no sea de solo lectura.",
            FileNotFoundException => "El archivo se movió o fue eliminado. Vuelve a seleccionarlo; si es una plantilla, abre Administrar plantillas.",
            DirectoryNotFoundException => "La carpeta ya no está disponible. Conecta la unidad o elige otra carpeta en Ajustes.",
            IOException => "Cierra el PDF o Excel si está abierto en otro programa, comprueba el espacio disponible y vuelve a intentarlo.",
            ArgumentException or InvalidDataException => "Revisa el dato o archivo indicado en el detalle. Corrígelo y vuelve a intentarlo.",
            _ => "Revisa los datos y archivos seleccionados. Si continúa el problema, copia este detalle para solicitar ayuda."
        };
        return $"No se pudo {action}.\nQué puedes hacer: {fix}\nDetalle: {error.Message}";
    }
}
