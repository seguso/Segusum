using Microsoft.AspNetCore.Mvc;
using Seg;

namespace Segusum.AspNetCore;

/// <summary>
/// Controller HTTP condiviso del motore. Un gioco fornisce soltanto la
/// factory dei propri World tramite AddSegusum in Program.cs.
/// </summary>
public sealed class SegusumController : ApiBase
{
    private readonly ISegusumWorldFactory worldFactory;

    public SegusumController(ISegusumWorldFactory worldFactory)
    {
        this.worldFactory = worldFactory;
    }

    public override WorldBase buildEmptyWorld(string lang)
        => worldFactory.Create(lang, tutorialMode: false);

    protected override WorldBase buildEmptyWorld(string lang, bool tutorialMode)
        => worldFactory.Create(lang, tutorialMode);

    [HttpPost, HttpGet]
    [Route("api/test")]
    public IActionResult test() => Ok("ok");

    [HttpGet]
    [Route("api/last")]
    public IActionResult last() => checkLastUsersImpl();

    [HttpPost]
    [Route("api/createUserAndStartGame")]
    public IActionResult createUserAndStartGame([FromBody] InputCreateUserAndStartGame2 i)
        => createUserAndStartGameImpl(i);

    [HttpPost]
    [Route("api/startNewGame")]
    public IActionResult startNewGame([FromBody] Credentials i)
        => startNewGameImpl(i);

    [HttpPost]
    [Route("api/setGameMode")]
    public IActionResult setGameMode([FromBody] GameModeInput i)
        => setGameModeImpl(i);

    [HttpPost]
    [Route("api/saveGameWithName")]
    public IActionResult saveGameWithName([FromBody] SaveGameWithNameInput i)
        => saveGameWithNameImpl(i);

    [HttpPost]
    [Route("api/loadGameWithName")]
    public IActionResult loadGameWithName([FromBody] SaveGameWithNameInput i)
        => loadGameWithNameImpl(i);

    [HttpPost]
    [Route("api/cancelTextInputAction")]
    public IActionResult cancelTextInputAction([FromBody] CancelTextInputInput i)
        => cancelTextInputImpl(i);

    [HttpPost]
    [Route("api/submitTextInputAction")]
    public IActionResult submitTextInputAction([FromBody] SubmitTextInputInput i)
        => submitTextInputImpl(i);

    [HttpPost]
    [Route("api/talkHere")]
    public IActionResult talkHere([FromBody] Credentials i)
        => talkHereImpl(i);

    [HttpPost]
    [Route("api/quickMove")]
    public IActionResult quickMove([FromBody] QuickMoveInput i)
        => quickMoveImpl(i);

    [HttpPost]
    [Route("api/useWith")]
    public IActionResult usewith([FromBody] UseWithActionInput i)
        => useWithImpl(i);

    [HttpPost]
    [Route("api/useFor")]
    public IActionResult useFor([FromBody] UseForInput i)
        => useForImpl(i);

    [HttpPost]
    [Route("api/tutorialPrompt")]
    public IActionResult tutorialPrompt([FromBody] TutorialPromptInput i)
        => tutorialPromptImpl(i);

    [HttpPost]
    [Route("api/isActually")]
    public IActionResult isActually([FromBody] IsActuallyInput i)
        => isActuallyImpl(i);

    [HttpPost]
    [Route("api/useHere")]
    public IActionResult useHere([FromBody] LookPickupRememberInput i)
        => lookPickupImpl(i, ispickup: false, isUseHere: true, isLook: false, isRemember: false);

    [HttpPost]
    [Route("api/useInComposer")]
    public IActionResult useWithComposerAction([FromBody] UseInComposerInput i)
        => useInComposerImpl(i);

    [HttpPost]
    [Route("api/getNextHint")]
    public IActionResult getNextHint([FromBody] GetNextHintInput i)
        => getNextHintImpl(i);

    [HttpPost]
    [Route("api/getCurrentHints")]
    public IActionResult getCurrentHints([FromBody] Credentials i)
        => getCurrentHintsImpl(i);

    [HttpPost]
    [Route("api/look")]
    public IActionResult look([FromBody] LookPickupRememberInput i)
        => lookPickupImpl(i, ispickup: false, isUseHere: false, isLook: true, isRemember: false);

    [HttpPost]
    [Route("api/pickup")]
    public IActionResult pickup([FromBody] LookPickupRememberInput i)
        => lookPickupImpl(i, ispickup: true, isUseHere: false, isLook: false, isRemember: false);

    [HttpPost]
    [Route("api/remember")]
    public IActionResult remember([FromBody] LookPickupRememberInput i)
        => lookPickupImpl(i, ispickup: false, isUseHere: false, isLook: false, isRemember: true);

    [HttpPost]
    [Route("api/replay_cut_scene")]
    public IActionResult replay_cut_scene([FromBody] ReplayCutSceneInput i)
        => replay_cut_scene_impl(i);

    [HttpPost]
    [Route("api/getNextAr")]
    public IActionResult getNextAr([FromBody] Credentials i)
        => getNextArImpl(i);

    [HttpPost]
    [Route("api/getPreviousCutSceneElement")]
    public IActionResult getPreviousCutSceneElement([FromBody] Credentials i)
        => getPreviousCutSceneElementImpl(i);

    [HttpPost]
    [Route("api/skipToEndOfCutScene")]
    public IActionResult skipToEndOfCutScene([FromBody] Credentials i)
        => skipToEndOfCutSceneImpl(i);

    [HttpPost]
    [Route("api/loadGame")]
    public IActionResult loadGame([FromBody] Credentials i)
        => loadGameImpl(i);

    [HttpPost]
    [Route("api/talkAction")]
    public IActionResult talkAction([FromBody] AskQuestionInput i)
        => talkActionImpl(i);

    [HttpPost]
    [Route("api/markObjectivesSeen")]
    public IActionResult markObjectivesSeen([FromBody] ObjectivesSeenInput i)
        => objectivesSeenImpl(i);
}
