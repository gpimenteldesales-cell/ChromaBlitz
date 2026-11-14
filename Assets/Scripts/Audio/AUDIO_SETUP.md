# CHROMA BLITZ — Audio na Unity (passo a passo)

> Se você importou o antigo `StemMusicController.cs`, apague: o `AudioManager` substitui ele.

## 1. Importar os arquivos
Do pacote `ChromaBlitz_Audio_Final_*.zip`, crie `Assets/Audio/Music`, `Assets/Audio/SFX`, `Assets/Audio/ColorNotes`.

| Tipo | Load Type | Compression | Observações |
|---|---|---|---|
| Stems da música (WAV) | Compressed In Memory | Vorbis (qualidade ~70) | Force To Mono: DESMARCADO |
| SFX e notas de cor | Decompress On Load | ADPCM (ou PCM) | Preload Audio Data: marcado |

## 2. Criar o AudioManager (UMA vez, na cena MainMenu)
1. GameObject vazio `AudioManager` + script **AudioManager**.
2. Arraste os clips:
   - Bass / Arp / Pad / Lead / Drums / Fx / Vhs -> `ChromaBlitz_Menu_bass`, `_arp`, `_pad`, `_lead`, `_drums`, `_fx`, `_vhs`
   - Hover (tamanho 3) -> `ui_hover_1`, `ui_hover_2`, `ui_hover_3`
   - Click, Back, Error, Correct -> `ui_click`, `ui_back`, `ui_error`, `ui_correct`
   - Jingle -> `hook_jingle`
   - Color Notes (tamanho 6, NESTA ordem) -> vermelho, amarelo, verde, ciano, azul, roxo
3. BPM = 75, Beats Per Bar = 4 (já é o padrão). Não deixe o fullmix: só os stems.

## 3. Botões do menu
Em cada botão (JOGAR, OPÇÕES, SAIR, VOLTAR): adicione **ButtonSfx**.
- JOGAR: Click Sound = Click, Change Music State = ✔, Music State = **Starting**
- OPÇÕES: Click, Change Music State = ✔, **Options**
- VOLTAR: Click Sound = **Back**, Change Music State = ✔, **Menu**
- SAIR: Click

## 4. Slider de volume das Opções
Troque o que o slider chama hoje por `AudioManager.Instance.SetMasterVolume(valor)`
(ou `SetMusicVolume` / `SetSfxVolume` se criar sliders separados). Os valores são salvos sozinhos.
Ao abrir as Opções, carregue o slider com `AudioManager.Instance.MasterVolume`.

## 5. Plataforma do menu no ritmo
Adicione **BeatColorPlatform** na plataforma (ou no objeto pai). Ela gira em sincronia com o loop,
troca as cores a cada batida e dá um pulso de escala. Se você já usa `ColorUtils`, preencha `Palette`
com as mesmas cores (a ordem é a das notas: vermelho, amarelo, verde, ciano, azul, roxo).
Desative o script antigo que trocava a cor a cada 1 segundo.

## 6. Clima da música por cena
Um **SceneMusicState** por cena: MainMenu = Menu, cena do jogo = Gameplay, tela de derrota = GameOver.

## 7. No jogo (GameManager)
```csharp
// acertou a cor:
AudioManager.Instance.PlayColorNote(corIndex);   // 0 vermelho ... 5 roxo
AudioManager.Instance.PlayCorrect();
// pisou na cor errada:
AudioManager.Instance.PlayError();
AudioManager.Instance.SetState(AudioManager.MusicState.GameOver, 0.8f);
// a cada aumento de dificuldade (0..1):
AudioManager.Instance.SetMusicSpeed(1f + 0.25f * dificuldade01, 2f);
// pausa / despausa:
AudioManager.Instance.SetState(AudioManager.MusicState.Paused, 0.3f);
AudioManager.Instance.SetState(AudioManager.MusicState.Gameplay, 0.3f);
```
Cuidado: `SetMusicSpeed` muda o pitch junto (efeito fita acelerando). Até 1.25 (~+4 semitons) soa bem;
acima disso a música sai da tonalidade. Para o tempo dobrar de verdade (75 -> 150 BPM) o ideal são
stems renderizados em andamentos diferentes.

## 8. VHS (opcional)
**VhsAudioLink** liga o chiado de fita ao valor do seu shader de VHS (material + nome da propriedade float).

## Eventos de ritmo (para qualquer script)
```csharp
AudioManager.Instance.OnBeat += beat => { /* a cada 0,8 s */ };
AudioManager.Instance.OnBar  += bar  => { /* a cada compasso */ };
float t = AudioManager.Instance.BeatProgress;   // 0..1 dentro da batida
```
