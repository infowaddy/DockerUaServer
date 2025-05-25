using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace DockerUaServer
{
    public interface IDockerUaServer
    {
        Task Start(CancellationToken cancellationToken);
        void Stop(CancellationToken cancellationToken);
    }
    public class DockerUaServer: StandardServer,IDockerUaServer
    {
        ILogger<DockerUaServer> logger;
        DockerUaServerSettings dockerUaServerSettings;
        private ICertificateValidator? m_userCertificateValidator ;
        public DockerUaServer(ILogger<DockerUaServer> _logger, IOptions<DockerUaServerSettings> _settingOption) { 
            this.logger = _logger;
            this.dockerUaServerSettings = _settingOption.Value;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            // Start the server
            try
            {
                // Step 1: Create and configure the OPC UA application instance
                ApplicationInstance application = new ApplicationInstance
                {
                    ApplicationName = dockerUaServerSettings.ApplicationName,
                    ApplicationType = ApplicationType.Server
                };

                // Step 2: Load the _configuration file
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("Operation canceled before starting OPC UA Server.");
                }

                ApplicationConfiguration opcUaConfiguration = await application.LoadApplicationConfiguration(dockerUaServerSettings.ServerConfigXML, false);
                logger.LogInformation("OPC UA Server Configuration file loaded successfully.");

                // Step 3: Check the application certificate
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("Operation canceled during certificate check.");
                }
                bool haveAppCertificate = await application.CheckApplicationInstanceCertificates(false, 0);
                logger.LogInformation("OPC UA Application instance certificate checked.");

                if (!haveAppCertificate)
                {
                    logger.LogError("OPC UA Application instance certificate is not valid.");
                }

                // process and command line arguments.
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("Operation canceled while processing command line arguments.");
                }
                if (application.ProcessCommandLine())
                {
                    logger.LogInformation("Command line arguments processed.");
                }

                // Step 4: Start the _server
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("Operation canceled before starting the _server.");
                }
                logger.LogInformation("Starting the Docker OPC UA Server...");
                await application.Start(this);

                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("Operation canceled after OPC UA Server started.");
                }
                logger.LogInformation("OPC UA Server started successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError($"{ex.Message}", ex);
            }
        }
        public void Stop(CancellationToken cancellationToken)
        {
            // Stop the server
            try
            {
                base.Stop();
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to stop OPC UA Server: {ex.Message}");
            }
        }

        protected override SessionManager CreateSessionManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            var sessionManager = new DockerUaServerSessionManager(server, configuration);

            // Attach the ImpersonateUser event to handle user validation
            sessionManager.ImpersonateUser += OnImpersonateUser;
            return sessionManager;
        }

        private void OnImpersonateUser(Session session, ImpersonateEventArgs args)
        {
            if (args.NewIdentity is UserNameIdentityToken usernameToken)
            {
                string username = usernameToken.UserName;
                string password = usernameToken.DecryptedPassword;

                // TODO: Do Username and Password validation with API Server
                if (username == password)
                {
                    IUserIdentity adminIdentity = new UserIdentity(usernameToken);
                    // TODO: Give GrantedRoleId as per your project
                    adminIdentity.GrantedRoleIds.Add(Role.SecurityAdmin.RoleId);
                    adminIdentity.GrantedRoleIds.Add(Role.ConfigureAdmin.RoleId);
                    adminIdentity.GrantedRoleIds.Add(Role.Engineer.RoleId);
                    args.Identity = adminIdentity;
                }
                else
                {
                    // Reject the unregistered user connection by throwing an exception
                    // Reject the login attempt with a custom error message
                    logger.LogError("Invalid username or password. Please try again.");
                    throw new ServiceResultException(StatusCodes.BadIdentityTokenRejected, "Invalid username or password. Please try again.");
                }
                return;
            }

            if (args.NewIdentity is X509IdentityToken x509IdentityToken)
            {
                VerifyUserTokenCertificate(x509IdentityToken.Certificate);
                args.Identity = new RoleBasedIdentity(new UserIdentity(x509IdentityToken), new List<Role> { Role.AuthenticatedUser });
                logger.LogInformation(512, "X509 Token Accepted: {0}", args.Identity?.DisplayName);

                var subject = x509IdentityToken.Certificate.Subject;
                var thumbprint = x509IdentityToken.Certificate.Thumbprint;

                string? cnValue = subject?.Split(",").FirstOrDefault(x => x.ToUpper().StartsWith("CN="));


                // Example: Use Subject string to determine user role
                // TODO: Implement according to your certificate policy and subject rules
                if (cnValue!.ToLower().Contains("adminuser"))
                {
                    IUserIdentity adminIdentity = new UserIdentity(x509IdentityToken);
                    adminIdentity.GrantedRoleIds.Add(Role.SecurityAdmin.RoleId);
                    adminIdentity.GrantedRoleIds.Add(Role.ConfigureAdmin.RoleId);
                    adminIdentity.GrantedRoleIds.Add(Role.Engineer.RoleId);
                    args.Identity = adminIdentity;
                }
                else
                {
                    logger.LogWarning("Certificate subject not recognized: {0}", subject);
                    throw new ServiceResultException(StatusCodes.BadIdentityTokenRejected, "Unknown certificate subject.");
                }

                logger.LogInformation("X509 Token Accepted. Subject: {0}", subject);
                return;
            }

            if (args.NewIdentity is IssuedIdentityToken issuedToken)
            {
                logger.LogError("Users try to login with IssuedIdentityToken which is not implemented.");
                throw ServiceResultException.Create(2149580800u, "Not supported user token type: {0}.", args.NewIdentity);
            }

            if (args.NewIdentity is AnonymousIdentityToken || args.NewIdentity == null)
            {
                // Reject the anonymous connection by throwing an exception
                throw new ServiceResultException(StatusCodes.BadUserAccessDenied, "Anonymous login is not allowed.");
            }

            logger.LogError("Not supported user token type: {0}.", args.NewIdentity);
            throw ServiceResultException.Create(2149580800u, "Not supported user token type: {0}.", args.NewIdentity);
        }

        private void CreateUserIdentityValidators(ApplicationConfiguration configuration)
        {
            for (int i = 0; i < configuration.ServerConfiguration.UserTokenPolicies.Count; i++)
            {
                if (configuration.ServerConfiguration.UserTokenPolicies[i].TokenType == UserTokenType.Certificate
                    && configuration.SecurityConfiguration.TrustedUserCertificates != null &&
                    configuration.SecurityConfiguration.UserIssuerCertificates != null)
                {
                    CertificateValidator certificateValidator = new CertificateValidator();
                    certificateValidator.UpdateAsync(configuration.SecurityConfiguration).Wait();
                    certificateValidator.Update(configuration.SecurityConfiguration.UserIssuerCertificates, configuration.SecurityConfiguration.TrustedUserCertificates, configuration.SecurityConfiguration.RejectedCertificateStore);
                    m_userCertificateValidator = certificateValidator.GetChannelValidator();
                }
            }
        }

        private void VerifyUserTokenCertificate(X509Certificate2 certificate)
        {
            try
            {
                if (m_userCertificateValidator != null)
                {
                    m_userCertificateValidator.Validate(certificate);
                }
                else
                {
                    base.CertificateValidator.Validate(certificate);
                }
            }
            catch (Exception ex)
            {
                StatusCode code = 2149646336u;
                TranslationInfo translationInfo;
                if (ex is ServiceResultException ex2 && ex2.StatusCode == 2149056512u)
                {
                    translationInfo = new TranslationInfo("InvalidCertificate", "en-US", "'{0}' is an invalid user certificate.", certificate.Subject);
                    code = 2149580800u;
                }
                else
                {
                    translationInfo = new TranslationInfo("UntrustedCertificate", "en-US", "'{0}' is not a trusted user certificate.", certificate.Subject);
                }

                throw new ServiceResultException(new ServiceResult(code, translationInfo.Key, LoadServerProperties().ProductUri, new LocalizedText(translationInfo)));
            }
        }
    }
}
