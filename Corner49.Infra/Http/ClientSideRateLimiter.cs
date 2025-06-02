using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Corner49.Infra.Http {

	internal sealed class ClientSideRateLimitedHandler : DelegatingHandler, IAsyncDisposable {
		private readonly RateLimiter _rateLimiter;

		public ClientSideRateLimitedHandler(RateLimiter limiter)
				: base(new HttpClientHandler()) {
			_rateLimiter = limiter;
		}

		// Override the SendAsync method to apply rate limiting.
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
			// Try to acquire a token from the rate limiter.
			using RateLimitLease lease = await _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);

			// If a token is acquired, proceed with sending the request.
			if (lease.IsAcquired) {
				return await base.SendAsync(request, cancellationToken);
			}

			// If no token could be acquired, simulate a 429 Too Many Requests response.
			var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

			// Add a 'Retry-After' header if the rate limiter provides a retry delay.
			if (lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)) {
				response.Headers.Add("Retry-After", ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo));
			}

			return response;
		}

		// Implement IAsyncDisposable to allow for asynchronous cleanup of resources.
		public async ValueTask DisposeAsync() {
			// Dispose of the rate limiter asynchronously.
			await _rateLimiter.DisposeAsync().ConfigureAwait(false);

			// Call the base Dispose method.
			Dispose(disposing: false);

			// Suppress finalization.
			GC.SuppressFinalize(this);
		}

		// Dispose pattern to clean up the rate limiter.
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);

			if (disposing) {
				// Synchronously dispose of the rate limiter if disposing is true.
				_rateLimiter.Dispose();
			}
		}
	}
}
