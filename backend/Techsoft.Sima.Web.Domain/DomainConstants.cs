namespace Techsoft.Sima.Web.Domain;

public static class DomainConstants
{
    public static class ExceptionCodes
    {
        public static class Operation
        {
            public const int USUARIO_YA_EXISTE = 11;
            public const int USUARIO_INACTIVO = 12;
            public const int SOLICITUD_YA_EXISTE = 13;
            public const int SOLICITUD_NO_PENDIENTE = 14;
            public const int ROL_NO_VALIDO = 15;
        }
    }

    public static class Values
    {
        public const int MAX_LENGTH_EMAIL = 256;
        public const int MAX_LENGTH_NOMBRE = 100;
        public const int MAX_LENGTH_APELLIDO = 100;
        public const int MAX_LENGTH_COMENTARIO = 500;
        public const int MAX_LENGTH_ROL = 50;
    }
}
