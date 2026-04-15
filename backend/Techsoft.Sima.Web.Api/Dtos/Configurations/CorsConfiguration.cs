namespace Techsoft.Sima.Web.Api.Dtos.Configurations;

public class CorsConfiguration
{
    public string[] Origins { get; }
    public string[] Headers { get; }

    public CorsConfiguration(string[] origins, string[] headers)
    {
        Origins = origins;
        Headers = headers;
    }
}
