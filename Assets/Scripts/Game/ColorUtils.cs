using UnityEngine;

public static class ColorUtils
{
    // =========================================================
    // ORIGINAIS (mantidos: o menu e o GameManager antigo usam)
    // =========================================================

    /// <summary>
    /// Gera uma cor RGB aleatória, evitando tons muito próximos do preto.
    /// </summary>
    public static Color RandomColorNoBlack(float minBrightness = 0.3f)
    {
        Color cor;
        do
        {
            cor = new Color(Random.value, Random.value, Random.value);
        }
        while (cor.grayscale < minBrightness);

        return cor;
    }

    /// <summary>
    /// Compara duas cores com uma pequena tolerância (evita erro de ponto flutuante).
    /// </summary>
    public static bool CoresIguais(Color a, Color b, float tolerancia = 0.01f)
    {
        return Mathf.Abs(a.r - b.r) < tolerancia &&
               Mathf.Abs(a.g - b.g) < tolerancia &&
               Mathf.Abs(a.b - b.b) < tolerancia;
    }

    // =========================================================
    // NOVOS: cores vivas e comparação justa
    // =========================================================

    /// <summary>
    /// Cor viva: matiz aleatório, saturação e brilho sempre altos.
    /// Nada de cinza, pastel ou tom "sujo" (que o RGB aleatório gera).
    /// </summary>
    public static Color RandomVibrant(float satMin = 0.65f, float valMin = 0.85f)
    {
        return Color.HSVToRGB(
            Random.value,
            Random.Range(Mathf.Clamp01(satMin), 1f),
            Random.Range(Mathf.Clamp01(valMin), 1f)
        );
    }

    /// <summary>
    /// Distância visual aproximada entre duas cores, de 0 (iguais) a ~1 (opostas).
    /// O matiz pesa mais, mas só quando a cor é saturada (em cinza, matiz não importa).
    /// </summary>
    public static float Distancia(Color a, Color b)
    {
        Color.RGBToHSV(a, out float h1, out float s1, out float v1);
        Color.RGBToHSV(b, out float h2, out float s2, out float v2);

        // Matiz é circular: 0.95 e 0.05 estão pertinho.
        float dh = Mathf.Abs(h1 - h2);
        if (dh > 0.5f) dh = 1f - dh;
        dh *= 2f;                                // 0..1

        float matiz = dh * Mathf.Min(s1, s2);
        float sat = Mathf.Abs(s1 - s2) * 0.6f;
        float brilho = Mathf.Abs(v1 - v2) * 0.6f;

        return Mathf.Sqrt(matiz * matiz + sat * sat + brilho * brilho);
    }

    /// <summary>
    /// Cor viva que fica a pelo menos "distanciaMin" de "alvo".
    /// Tenta várias vezes; se não conseguir, devolve a mais distante que achou.
    /// </summary>
    public static Color RandomVibrantAwayFrom(
        Color alvo,
        float distanciaMin,
        float satMin = 0.65f,
        float valMin = 0.85f,
        int tentativas = 60)
    {
        Color melhor = RandomVibrant(satMin, valMin);
        float melhorDist = Distancia(melhor, alvo);

        if (melhorDist >= distanciaMin)
            return melhor;

        for (int i = 0; i < tentativas; i++)
        {
            Color c = RandomVibrant(satMin, valMin);
            float d = Distancia(c, alvo);

            if (d >= distanciaMin)
                return c;

            if (d > melhorDist)
            {
                melhor = c;
                melhorDist = d;
            }
        }

        return melhor;
    }
}