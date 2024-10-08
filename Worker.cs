using TdLib;
using static TdLib.TdApi;
using static TdLib.TdApi.AuthorizationState;
using static TdLib.TdApi.MessageOrigin;
using File = System.IO.File;

namespace TgReposter
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var client = new TdClient();
            int apiId = 29968889;
            string apiHash = "eaa3ed72ba7aa29412515b2e0cc857e2";

            await client.ExecuteAsync(new SetTdlibParameters
            {
                ApiId = apiId,
                ApiHash = apiHash,
                SystemLanguageCode = "en",
                DeviceModel = "Desktop",
                ApplicationVersion = "1.0",
                UseMessageDatabase = true,
                UseSecretChats = false,
                DatabaseDirectory = "tdlib"
            });

            await client.SetLogVerbosityLevelAsync(0);

            AuthorizationState authState = await client.ExecuteAsync(new GetAuthorizationState());

            if (authState is not AuthorizationStateReady)
            {
                await Authorize(client);
            }

            static async Task Authorize(TdClient client)
            {
                Console.WriteLine("Введите номер телефона для авторизации:");
                string phoneNumber = Console.ReadLine() ?? string.Empty;

                await client.ExecuteAsync(
                    new SetAuthenticationPhoneNumber
                    {
                        PhoneNumber = phoneNumber
                    });

                Console.WriteLine("Введите код из SMS:");
                string smsCode = Console.ReadLine() ?? string.Empty;

                await client.ExecuteAsync(new TdApi.CheckAuthenticationCode
                {
                    Code = smsCode
                });

                Console.WriteLine("Успешная авторизация!");
            }

            var channelId = -1002216406420;
            var targetChannelId = -1002260607843;

            string lastMessageIdFile = "last_message_id.txt";
            long lysak = -1001765482253;
            long lukashchuk = -1001411992881;
            long zelia = -1001463721328;
            long test = 6658708977;
            List<long> filteredChannelIds = [lysak, lukashchuk, zelia, test];
            long lastProcessedMessageId = 0;

            if (File.Exists(lastMessageIdFile))
            {
                var lastMessageIdString = File.ReadAllText(lastMessageIdFile);
                long.TryParse(lastMessageIdString, out lastProcessedMessageId);
            }

            while (true)
            {
                var updates = await client.ExecuteAsync(new TdApi.GetChatHistory
                {
                    ChatId = channelId,
                    Limit = 100
                });

                foreach (var message in updates.Messages_ ?? Array.Empty<Message>())
                {
                    if (message.Id > lastProcessedMessageId)
                    {
                        if (message.ForwardInfo != null)
                        {
                            long originId = GetOriginChatId(message.ForwardInfo.Origin);

                            if (filteredChannelIds.Contains(originId))
                            {
                                Console.WriteLine($"Пропущено сообщение {message.Id}, репост из фильтрованного канала.");
                                continue;
                            }
                            else
                            {
                                await client.ExecuteAsync(
                                    new ForwardMessages
                                    {
                                        ChatId = targetChannelId,
                                        FromChatId = channelId,
                                        MessageIds = [message.Id],
                                        SendCopy = false,
                                        RemoveCaption = false
                                    });

                                Console.WriteLine($"Переслано репостное сообщение {message.Id}.");
                            }
                        }
                        else
                        {
                            await client.ExecuteAsync(
                                new ForwardMessages
                                {
                                    ChatId = targetChannelId,
                                    FromChatId = channelId,
                                    MessageIds = [message.Id],
                                    SendCopy = true,
                                    RemoveCaption = false
                                });

                            Console.WriteLine($"Переслано обычное сообщение {message.Id}.");
                        }

                        lastProcessedMessageId = message.Id;
                        File.WriteAllText(lastMessageIdFile, lastProcessedMessageId.ToString());
                    }

                    await Task.Delay(5000, stoppingToken);

                    static long GetOriginChatId(MessageOrigin origin)
                    {
                        if (origin.DataType == "messageOriginChannel")
                        {
                            return (origin as MessageOriginChannel)?.ChatId ?? 0;
                        }

                        if (origin.DataType == "messageOriginUser")
                        {
                            return (origin as MessageOriginUser)?.SenderUserId ?? 0;
                        }

                        return 0;
                    }
                }
            }
        }
    }
}