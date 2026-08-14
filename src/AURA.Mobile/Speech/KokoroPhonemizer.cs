using System;
using System.Collections.Generic;
using System.Linq;

namespace AURA.Mobile.Speech
{
    /// <summary>
    /// Converte texto (pt-br) em uma string de fonemas IPA compatível com o
    /// vocabulário do Kokoro. É a etapa G2P que, no desktop, usa espeak-ng.
    ///
    /// ESTADO ATUAL (teste com frase fixa): o fonemizador embute um pequeno
    /// dicionário de frases de teste pré-fonemizadas offline (via espeak-ng).
    /// A integração com o espeak-ng nativo no Android (fonemização de texto
    /// arbitrário para a conversação) é o próximo passo, fora deste milestone.
    /// </summary>
    public sealed class KokoroPhonemizer
    {
        private readonly IReadOnlyDictionary<string, string> _phrases;

        public KokoroPhonemizer()
        {
            // Frases de teste fonemizadas offline com espeak-ng -v pt-br
            // (preserve_punctuation + with_stress, iguais ao kokoro-onnx).
            _phrases = new Dictionary<string, string>
            {
                ["Não sonhe sua vida, viva seu sonho."] =
                    "nˌɐ̃ʊ̃ sˈoɲy sˌuæ vˈidæ, vˈivæ seʊ sˈoɲʊ.",
                ["Olá! Eu sou a AURA, sua assistente pessoal."] =
                    "olˈa! eʊ sow a ˈaʊɾæ, sˌuæ ˌasistˈeɪŋtʃy pˌesoˈaʊ.",
            };
        }

        /// <summary>Número de frases de teste disponíveis (para debug/UI).</summary>
        public int Count => _phrases.Count;

        /// <summary>
        /// Retorna a string de fonemas para o texto. Lança NotSupportedException
        /// para textos fora do dicionário (fonemizador completo ainda não
        /// integrado no Android).
        /// </summary>
        public string Phonemize(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            string key = text.Trim();
            if (_phrases.TryGetValue(key, out string? phonemes))
            {
                return phonemes;
            }

            throw new NotSupportedException(
                "O fonemizador atual só cobre frases de teste. " +
                "Frase não reconhecida: \"" + key + "\"");
        }
    }
}
