﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace SysBot.ACNHOrders
{
    public static class Globals
    {
        public static SysCord Self { get; set; } = default!;
        public static CrossBot Bot { get; set; } = default!;
        public static QueueHub Hub { get; set; } = default!;
    }

    public sealed class RequireQueueRoleAttribute : PreconditionAttribute
    {
        // Create a field to store the specified name
        private readonly string _name;

        // Create a constructor so the name can be specified
        public RequireQueueRoleAttribute(string name) => _name = name;

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var mgr = Globals.Bot.Config;
            if (mgr.CanUseSudo(context.User.Id) || Globals.Self.Owner == context.User.Id || mgr.IgnoreAllPermissions)
                return Task.FromResult(PreconditionResult.FromSuccess());

            // Check if this user is a Guild User, which is the only context where roles exist
            if (context.User is not SocketGuildUser gUser)
                return Task.FromResult(PreconditionResult.FromError("Necesitas estar en un gremio para ejecutar este comando."));

            if (!mgr.AcceptingCommands)
                return Task.FromResult(PreconditionResult.FromError("Lo siento, actualmente no acepto comandos."));

            bool hasRole = mgr.GetHasRole(_name, gUser.Roles.Select(z => z.Name));
            if (!hasRole)
                return Task.FromResult(PreconditionResult.FromError("No tienes el rol necesario para ejecutar este comando."));

            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
