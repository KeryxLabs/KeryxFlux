using System.Globalization;
using System.Text.Json.Serialization;

namespace KeryxFlux.Domain.Models.Http
{
    public class AuthToken
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
        [JsonPropertyName("userName")]
        public string? UserName { get; set; }
        [JsonPropertyName(".issued")]
        public string? Issue { get; set; }
        [JsonPropertyName(".expires")]
        public string? ExpiresOn { get; set; }
        [JsonPropertyName("expiry_date")]
        public string? ExpiryDate { get; set; }

        [JsonIgnore]
        public DateTime TimeToRefresh
        {
            get
            {
                return DateTime.UtcNow.AddSeconds(Convert.ToInt32(ExpiresIn) * 0.9);
            }
        }
        [JsonIgnore]
        public bool IsExpired
        {
            get
            {
                var expDate = Convert.ToDateTime(ExpiresOn, CultureInfo.InvariantCulture);
                return expDate < DateTime.UtcNow;
            }
        }

    }
}
