using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Extensions;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.Json;


namespace KeryxFlux.Domain.Models.Http
{
    public sealed record HttpServerRequest
    {
        private string _previousPathTemplate = string.Empty;
        private string _currentPathTemplate = string.Empty;
        private readonly Authorization _authorization;
        private readonly Uri _baseAddress;
        private readonly Pagination? _pagination;
        private readonly List<RetriableError> _retriableErrors;
        private readonly ServerInformation _serverInformation;
        private int _currentPageNumber = 0;
        private byte[] _response = [];
        private int _estimatedContent = 0;
        private bool HasPagination => _serverInformation.Http?.Pagination is not null;
        private bool SupportsRefreshToken => _serverInformation.Authorization.Refreshes;
        private int MaxPageNumber => _serverInformation.Http?.Pagination?.MaxPageNumber ?? 10;
        private int MaxPageSize => _serverInformation.Http?.Pagination?.MaxPageSize ?? 50;

        public string Name { get; set; }
        public AuthToken AuthToken { get; private set; }
        public string AuthPath => _serverInformation.Authorization.Path;
        public int Timeout => _serverInformation.Timeout;
        public Uri RequestUri => _baseAddress;
        public Uri AuthUri => !string.IsNullOrWhiteSpace(_serverInformation.Authorization.BaseUrl) ? new Uri(_serverInformation.Authorization.BaseUrl) : _baseAddress;

        public HttpServerRequest(string name, ServerInformation serverInfo,string path)
        {
            Name = name;
            _serverInformation = serverInfo;
            _baseAddress = new(serverInfo.Address);
            _currentPathTemplate = path;
            _authorization = serverInfo.Authorization;
            _pagination = serverInfo.Http?.Pagination;
            _retriableErrors = serverInfo.RetriableErrors;
            AuthToken = new();
        }

        public bool IsErrorRetriable(string content, out RetriableError? error) => _retriableErrors.IsRetriableError(content, out error);

        public void UpdateFromStep(Step step)
        {
            _currentPageNumber = 0;
            _estimatedContent = 0;
            _previousPathTemplate = _currentPathTemplate;
            _currentPathTemplate = step.Request;
        }

        public void SetResponse(byte[] response)
        {
            _response = response;
        }

        public Result TrySetToken(string tokenStr)
        {
            try
            {
                var newToken = JsonSerializer.Deserialize<AuthToken>(tokenStr);
                if (newToken is null)
                    return ProcessingError.EmptyAuthToken;

                AuthToken = newToken;
                return Result.Success();

            }
            catch (Exception ex)
            {
                return ProcessingError.InvalidAuthToken(ex.Message);
            }
        }

        public string AuthorizationHeader
        {
            get
            {
                if (!AuthToken.IsExpired && !string.IsNullOrWhiteSpace(AuthToken.AccessToken))
                    return "Bearer " + AuthToken.AccessToken;
                return _authorization.AuthorizationType switch
                {
                    AuthorizationType.Basic => "Basic " + _authorization.Credentials["EncodedSecret"],
                    AuthorizationType.Bearer => "Bearer " + AuthToken.AccessToken,
                    _ => string.Empty,
                };
            }
        }

        public FormUrlEncodedContent AuthorizationData
        {
            get
            {
                var credentials = _authorization.Credentials;

                var requestData = AuthToken is not null and { IsExpired: true } && SupportsRefreshToken ?
                                  credentials
                                  .Append(new("grant_type", "refresh_token"))
                                  .Append(new("refresh_token", AuthToken.RefreshToken ?? string.Empty)).ToList()
                                  : [.. credentials];

                return new FormUrlEncodedContent(requestData);
            }
        }

        public IEnumerable<string> AvailablePaths
        {
            get
            {
                while (CanPaginate)
                {
                    _estimatedContent += MaxPageSize;
                    _currentPageNumber++;
                    yield return _currentPathTemplate
                                    .Replace(InternalConstants.PageSizeTemplate, $"{MaxPageSize}")
                                    .Replace(InternalConstants.PageNumberTemplate, $"{_currentPageNumber}");
                }
            }
        }


        private bool CanPaginate
        {
            get
            {
                if (string.IsNullOrEmpty(_currentPathTemplate)) return false;
                if (_response.Length == 0 && _estimatedContent == 0 && _previousPathTemplate != _currentPathTemplate) return true;
                if (_response.Length == 0 && (_estimatedContent < 0 || _estimatedContent > MaxPageSize)) return false;
                if (!HasPagination) return false;
                if (_currentPageNumber > MaxPageNumber) return false;

                try
                {
                    var model = JsonSerializer.Deserialize<JsonElement>(_response.AsSerializableStr());
                    if (!model.TryGetProperty(_pagination!.PaginationProperty, out var paginationProperty))
                        return false;

                    var propStr = paginationProperty.ToString();
                    if (string.IsNullOrWhiteSpace(propStr)) return false;

                    return _pagination!.PaginationType switch
                    {
                        PaginationType.RecordCount => int.TryParse(propStr, out var recordCount) && _estimatedContent < recordCount && recordCount > 0,
                        PaginationType.PageCount => int.TryParse(propStr, out var pageCount) && _currentPageNumber < pageCount && pageCount > 0,
                        _ => false
                    };

                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

    }
}
