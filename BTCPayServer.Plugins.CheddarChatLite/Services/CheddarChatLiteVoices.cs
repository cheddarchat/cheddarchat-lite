using System.Collections.Generic;
using System.Linq;

namespace BTCPayServer.Plugins.CheddarChatLite.Services;

public record CheddarChatLiteVoice(string Id, string Name, string Group);

/// <summary>
/// The English voices of the Phonon text-to-speech model, which the overlay runs in the OBS browser source.
/// Ids are the file names in https://huggingface.co/gradium/phonon-voices (Apache-2.0).
/// </summary>
public static class CheddarChatLiteVoices
{
    public const string Default = "LMDIYE0Oa377Qh6Z"; // Easton

    public static readonly IReadOnlyList<CheddarChatLiteVoice> All =
    [
        new("LMDIYE0Oa377Qh6Z", "Easton (male)", "American"),
        new("NbpkqMVS3CJeq2j8", "Zoey (female)", "American"),
        new("NIWIOv5vTi5eRMj8", "Juniper (female)", "American"),
        new("vjFyp2BcCyTlUmYb", "Everly (female)", "American"),
        new("4SZHfMpw-p46Ywgs", "Harper (female)", "American"),
        new("bXwrkHhURL1VFJdG", "Hadley (female)", "American"),
        new("nGzMHMPAWGaJFHOo", "Delia (female)", "American"),
        new("40QrMVgAxFiUnP9f", "Micah (male)", "American"),
        new("0zLApsZfVLLNaEGl", "Trevor (male)", "American"),
        new("_6Aslh2DxfmnRLmP", "Russell (male)", "American"),
        new("qIxlZ7Ei8KvWlyQH", "Preston (male)", "American"),
        new("d9XCyhjo4JhkHF1d", "Miles (male)", "American"),
        new("6PWnV0Nq4wu7RVBT", "Maeve (female)", "British"),
        new("GgfEkEJtxZR7gnpy", "Freya (female)", "British"),
        new("uem82D50GRv2Dwma", "Pippa (female)", "British"),
        new("y9bQqIDnyxwqK01t", "Elodie-Rose (female)", "British"),
        new("gDT1nz7Ie36ZhL-C", "Imogen (female)", "British"),
        new("m9MxpY6IUGXtWp76", "Lottie (female)", "British"),
        new("X6NceqF285Gr04gb", "Alfie (male)", "British"),
        new("YyKcxPG7iIgS3jsK", "Hugh (male)", "British"),
        new("zUCxzrOrugTSis03", "Edwin (male)", "British"),
        new("JOW0vchyswkQUMDS", "Spencer (male)", "British"),
        new("dME3IWyZBvmh1n1q", "Toby (male)", "British"),
        new("Zn7g6NxLqXlJb7Qi", "Teddy (male)", "British"),
        new("gfP44RlHwr2kEwk7", "Bryony (female)", "British"),
        new("xPDpsV3F592srkjA", "Captain Silas Blackwater", "Characters"),
        new("nOykTmcH9ydsGGIc", "Morwenna of the Deep", "Characters"),
        new("lX3LavMNdlIbgEHH", "Old Mortlake", "Characters"),
        new("dGyGDKo2osj7h3s7", "Azgorath", "Characters"),
        new("cXoIXJ2llS8S0WOW", "Fennick the Young Elf", "Characters"),
        new("GXaSqMEmOg12OIYb", "The Thing Under the Stairs", "Characters"),
        new("xO8xQo3BrB8FuI6J", "Vyrmathrax the Ancient", "Characters"),
        new("cYtsRSxV5q9IMehn", "Grub the Ogre", "Characters"),
        new("gSgG5Tor0GPEnqzC", "Master Aldric", "Characters"),
        new("NIWoEurJHMJjyUTe", "The Snow Queen", "Characters"),
        new("y2eiKxtQo2B67deU", "Sir Bramwell", "Characters"),
        new("QXNr0SvkukLB4wPq", "Fairy Godmother", "Characters"),
    ];

    public static bool IsValid(string? id) => All.Any(voice => voice.Id == id);
}
