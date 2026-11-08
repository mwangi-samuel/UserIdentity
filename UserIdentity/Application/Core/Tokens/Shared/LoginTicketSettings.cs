using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.Tokens.Shared;

public record LoginTicketSettings
{
    [Range(1, int.MaxValue)]
    public int ValidForSeconds { get; set; }
}
