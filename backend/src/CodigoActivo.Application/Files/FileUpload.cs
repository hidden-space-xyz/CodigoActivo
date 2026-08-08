namespace CodigoActivo.Application.Files;

public sealed record FileUpload(Stream Content, string FileName, long Length);
