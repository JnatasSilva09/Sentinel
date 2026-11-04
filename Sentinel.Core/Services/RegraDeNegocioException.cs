namespace Sentinel.Core.Services;

/// <summary>Violação de regra de negócio, com mensagem pronta para exibir ao usuário.</summary>
public class RegraDeNegocioException : Exception
{
    public RegraDeNegocioException(string mensagem) : base(mensagem) { }
}
