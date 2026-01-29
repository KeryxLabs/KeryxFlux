using KeryxFlux.Domain.Abstractions;


namespace KeryxFlux.Domain.Models.Sftp
{
    public record SftpEntry(string Name) : RunnableEntry(Name)
    {
    }
}
