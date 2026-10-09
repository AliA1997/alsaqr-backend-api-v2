using System.Text;
using AlSaqr.Infrastructure.Config;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

namespace AlSaqr.Tests.Support
{
    /// <summary>
    /// A throwaway Galène server built from docker/galene. One per test class;
    /// every test uses its own room name, so tests cannot see each other.
    /// </summary>
    public sealed class GaleneContainer : IAsyncLifetime
    {
        public const string AdminUser = "alsaqr-admin";
        public const string AdminPassword = "test-galene-admin-password";
        public const string TokenSecret = "test-galene-token-secret-0123456";

        private const string ConfigJson =
            "{\"writableGroups\":true,\"users\":{\"" + AdminUser + "\":" +
            "{\"password\":\"" + AdminPassword + "\",\"permissions\":\"admin\"}}}";

        private readonly IFutureDockerImage _image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "docker/galene")
            .WithDockerfile("Dockerfile")
            .WithName("alsaqr-galene:test")
            .WithCleanUp(false)   // the Go build is the slow part; keep the image between runs
            .Build();

        private IContainer _container = default!;

        public string BaseUrl { get; private set; } = default!;

        public GaleneConfig Config { get; private set; } = default!;

        public async Task InitializeAsync()
        {
            await _image.CreateAsync();

            _container = new ContainerBuilder()
                .WithImage(_image)
                .WithResourceMapping(Encoding.UTF8.GetBytes(ConfigJson), "/srv/galene/data/config.json")
                // Plain HTTP for tests; no TURN (containers reach each other directly).
                .WithCommand("-insecure", "-http", ":8443", "-turn", "")
                .WithPortBinding(8443, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request.ForPort(8443).ForPath("/")))
                .Build();

            await _container.StartAsync();

            BaseUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8443)}";
            Config = new GaleneConfig
            {
                AdminUrl = BaseUrl,
                PublicUrl = BaseUrl,
                AdminUsername = AdminUser,
                AdminPassword = AdminPassword,
                TokenSecret = TokenSecret,
            };
        }

        public async Task DisposeAsync() => await _container.DisposeAsync();
    }
}
