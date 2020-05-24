﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GitTrends.Mobile.Shared;
using GitTrends.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Refit;

namespace GitTrends.UnitTests
{
    class GitHubGraphQLApiServiceTests : BaseTest
    {
        [Test]
        public void GetCurrentUserInfoTest_Unauthenticated()
        {
            //Arrange
            var githubGraphQLApiService = ContainerService.Container.GetService<GitHubGraphQLApiService>();

            //Act
            var exception = Assert.ThrowsAsync<ApiException>(async () => await githubGraphQLApiService.GetCurrentUserInfo(CancellationToken.None).ConfigureAwait(false));

            //Assert
            Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        }

        [Test]
        public void GetCurrentUserInfoTest_DemoUser()
        {
            //Arrange
            var githubGraphQLApiService = ContainerService.Container.GetService<GitHubGraphQLApiService>();
            var gitHubAuthenticationService = ContainerService.Container.GetService<GitHubAuthenticationService>();

            //Act
            var exception = Assert.ThrowsAsync<ApiException>(async () => await githubGraphQLApiService.GetCurrentUserInfo(CancellationToken.None).ConfigureAwait(false));

            //Assert
            Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        }

        [Test]
        public async Task GetCurrentUserInfoTest_AuthenticatedUser()
        {
            //Arrange
            var githubGraphQLApiService = ContainerService.Container.GetService<GitHubGraphQLApiService>();
            var gitHubUserService = ContainerService.Container.GetService<GitHubUserService>();

            //Act
            await AuthenticateUser(gitHubUserService, githubGraphQLApiService).ConfigureAwait(false);

            var (login, name, avatarUri) = await githubGraphQLApiService.GetCurrentUserInfo(CancellationToken.None).ConfigureAwait(false);

            //Assert
            Assert.AreEqual(AuthenticatedGitHubUserLogin, login);
            Assert.AreEqual(AuthenticatedGitHubUserName, name);
            Assert.AreEqual(new Uri(AuthenticatedGitHubUserAvatarUrl), avatarUri);
        }

        [Test]
        public void GetRepositoriesTest_Unauthenticated()
        {
            //Arrange
            var githubGraphQLApiService = ContainerService.Container.GetService<GitHubGraphQLApiService>();

            //Act
            var exception = Assert.ThrowsAsync<ApiException>(async () =>
            {
                await foreach (var retrievedRepositories in githubGraphQLApiService.GetRepositories(AuthenticatedGitHubUserLogin, CancellationToken.None).ConfigureAwait(false))
                {

                }
            });

            //Assert
            Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        }

        [Test]
        public async Task GetRepositoriesTest_DemoUser()
        {
            //Arrange
            List<Repository> repositories = new List<Repository>();
            var githubGraphQLApiService = ContainerService.Container.GetService<GitHubGraphQLApiService>();
            var gitHubAuthenticationService = ContainerService.Container.GetService<GitHubAuthenticationService>();

            //Act
            await gitHubAuthenticationService.ActivateDemoUser().ConfigureAwait(false);

            await foreach (var retrievedRepositories in githubGraphQLApiService.GetRepositories(AuthenticatedGitHubUserLogin, CancellationToken.None).ConfigureAwait(false))
            {
                repositories.AddRange(retrievedRepositories);
            }

            //Assert
            Assert.IsNotEmpty(repositories);
            Assert.AreEqual(DemoDataConstants.RepoCount, repositories.Count);

            foreach (var repository in repositories)
            {
                Assert.GreaterOrEqual(DemoDataConstants.MaximumRandomNumber, repository.IssuesCount);
                Assert.GreaterOrEqual(DemoDataConstants.MaximumRandomNumber, repository.ForkCount);
                Assert.AreEqual(DemoDataConstants.Alias, repository.OwnerLogin);
                Assert.IsEmpty(repository.DailyClonesList);
                Assert.IsEmpty(repository.DailyViewsList);
                Assert.AreEqual(repository.TotalClones, 0);
                Assert.AreEqual(repository.TotalUniqueClones, 0);
                Assert.AreEqual(repository.TotalViews, 0);
                Assert.AreEqual(repository.TotalUniqueViews, 0);
            }
        }

        [Test]
        public async Task GetRepositoriesTest_AuthenticatedUser()
        {
            //Arrange
            List<Repository> repositories = new List<Repository>();
            var githubGraphQLApiService = ContainerService.Container.GetService<GitHubGraphQLApiService>();
            var gitHubUserService = ContainerService.Container.GetService<GitHubUserService>();

            //Act
            await AuthenticateUser(gitHubUserService, githubGraphQLApiService).ConfigureAwait(false);

            await foreach (var retrievedRepositories in githubGraphQLApiService.GetRepositories(AuthenticatedGitHubUserLogin, CancellationToken.None).ConfigureAwait(false))
            {
                repositories.AddRange(retrievedRepositories);
            }

            //Assert
            Assert.GreaterOrEqual(300, repositories.Count);

            Assert.IsTrue(repositories.Any(x => x.Name is ValidGitHubRepo));
            Assert.IsTrue(repositories.Any(x => x.OwnerLogin is AuthenticatedGitHubUserLogin));
            Assert.IsTrue(repositories.Any(x => x.OwnerAvatarUrl is AuthenticatedGitHubUserAvatarUrl));

            Assert.IsEmpty(repositories.SelectMany(x => x.DailyClonesList));
            Assert.IsEmpty(repositories.SelectMany(x => x.DailyViewsList));
        }

        [Test]
        public void GetRepositoryTest_Unauthenticated()
        {
            //Arrange
            var githubGraphQLApiService = ContainerService.Container.GetService<GitHubGraphQLApiService>();

            //Act
            var exception = Assert.ThrowsAsync<ApiException>(async () => await githubGraphQLApiService.GetRepository(AuthenticatedGitHubUserName, ValidGitHubRepo, CancellationToken.None).ConfigureAwait(false));

            //Assert
            Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        }

        [Test]
        public async Task GetRepositoryTest_Demo()
        {
            //Arrange
            var githubGraphQLApiService = ContainerService.Container.GetService<GitHubGraphQLApiService>();
            var gitHubAuthenticationService = ContainerService.Container.GetService<GitHubAuthenticationService>();

            //Act
            await gitHubAuthenticationService.ActivateDemoUser().ConfigureAwait(false);
            var exception = Assert.ThrowsAsync<ApiException>(async () => await githubGraphQLApiService.GetRepository(AuthenticatedGitHubUserName, ValidGitHubRepo, CancellationToken.None).ConfigureAwait(false));

            //Assert
            Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        }

        [Test]
        public async Task GetRepositoryTest_AuthenticatedUser()
        {
            //Arrange
            Repository repository;
            DateTimeOffset beforeDownload, afterDownload;

            var githubGraphQLApiService = ContainerService.Container.GetService<GitHubGraphQLApiService>();
            var gitHubUserService = ContainerService.Container.GetService<GitHubUserService>();

            await AuthenticateUser(gitHubUserService, githubGraphQLApiService).ConfigureAwait(false);

            //Act
            beforeDownload = DateTimeOffset.UtcNow;

            repository = await githubGraphQLApiService.GetRepository(AuthenticatedGitHubUserLogin, ValidGitHubRepo, CancellationToken.None).ConfigureAwait(false);

            afterDownload = DateTimeOffset.UtcNow;

            //Assert
            Assert.Greater(repository.StarCount, 150);
            Assert.Greater(repository.ForkCount, 30);

            Assert.AreEqual(ValidGitHubRepo, repository.Name);
            Assert.AreEqual(AuthenticatedGitHubUserLogin, repository.OwnerLogin);
            Assert.AreEqual(AuthenticatedGitHubUserAvatarUrl, repository.OwnerAvatarUrl);

            Assert.IsEmpty(repository.DailyClonesList);
            Assert.IsEmpty(repository.DailyViewsList);

            Assert.AreEqual(0, repository.TotalClones);
            Assert.AreEqual(0, repository.TotalUniqueClones);
            Assert.AreEqual(0, repository.TotalViews);
            Assert.AreEqual(0, repository.TotalUniqueViews);

            Assert.IsTrue(beforeDownload.CompareTo(repository.DataDownloadedAt) < 0);
            Assert.IsTrue(afterDownload.CompareTo(repository.DataDownloadedAt) > 0);

            Assert.IsFalse(string.IsNullOrWhiteSpace(repository.Description));

            Assert.IsFalse(repository.IsFork);
        }
    }
}