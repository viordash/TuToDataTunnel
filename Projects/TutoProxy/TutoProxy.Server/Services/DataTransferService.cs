namespace TutoProxy.Server.Services {
    public interface IDataTransferService {
        Task<TResponseModel> SendAsync<TResponseModel, TRequestModel>(TRequestModel request) where TResponseModel : new();
    }


    public class DataTransferService : IDataTransferService {
        readonly ILogger logger;
        public DataTransferService(ILogger logger) {
            Guard.NotNull(logger, nameof(logger));
            this.logger = logger;
        }

        public Task<TResponseModel> SendAsync<TResponseModel, TRequestModel>(TRequestModel request) where TResponseModel : new() {
            logger.Information($"Send :{request}");

            return Task.FromResult(new TResponseModel());
        }
    }
}
