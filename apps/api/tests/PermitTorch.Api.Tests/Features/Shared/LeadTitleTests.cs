using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Tests.Features.Shared;

// A lead's title is the start of what the permit says the work is, tidied so it can be read
// at a glance. Every description here is made up, in the shapes the sources send.
public class LeadTitleTests
{
    private static string Title(string? description, string? permitType = "fire_sprinkler",
        FireCategory category = FireCategory.FireSprinkler) => LeadTitle.From(description, permitType, category);

    [Fact]
    public void The_title_is_the_permits_own_words_for_the_work()
    {
        Assert.Equal("Relocate / add 21 sprinklers for new pharmacy",
            Title("Relocate / add 21 sprinklers for new pharmacy"));
    }

    [Theory]
    [InlineData("Relocate / add 21 sprinklers for new pharmacy | Fire Sprinkler and Suppression",
        "Relocate / add 21 sprinklers for new pharmacy")]
    [InlineData("Fire Alarm Permit | Provide a new addressable fire alarm system for the tenant space",
        "Provide a new addressable fire alarm system for the tenant space")]
    [InlineData("Install new fire alarm system | Fire Alarm | Electrical Fire Alarms",
        "Install new fire alarm system")]
    public void Of_the_parts_a_source_sends_the_fullest_one_is_used(string description, string expected)
    {
        Assert.Equal(expected, Title(description));
    }

    [Fact]
    public void A_list_of_parts_without_spaces_reads_as_a_list()
    {
        Assert.Equal("Backflow preventer, fire pump, hose valve/outlet",
            Title("NEW CONSTRUCTION | BACKFLOW PREVENTER|FIRE PUMP|HOSE VALVE/OUTLET"));
    }

    [Fact]
    public void Only_the_first_sentence_is_used()
    {
        Assert.Equal("New 144 unit multi-family development of six buildings",
            Title("New 144 unit multi-family development of six buildings. Includes a detached leasing office. All work per plans."));
    }

    // A description often opens with a note to the permit office. The work is what follows.
    [Theory]
    [InlineData("THIS IS AN EXPEDITED THIRD-PARTY PLAN REVIEW. Add/Relocate 11 Sprinklers in the storage room area.",
        "Add/Relocate 11 Sprinklers in the storage room area")]
    [InlineData("Phase two review only. Scope of work includes a new elevator. Wire the fire pump and jockey pump.",
        "Scope of work includes a new elevator")]
    [InlineData("This is a permit revision to the approved permit B0000001. Add a standpipe to stair 2.",
        "Add a standpipe to stair 2")]
    public void A_note_to_the_permit_office_is_passed_over_for_the_work(string description, string expected)
    {
        Assert.Equal(expected, Title(description));
    }

    [Fact]
    public void A_note_to_the_permit_office_is_the_title_when_it_is_all_there_is()
    {
        Assert.Equal("Resubmittal of the fire alarm drawings",
            Title("Resubmittal of the fire alarm drawings."));
    }

    [Fact]
    public void A_sentence_too_short_to_say_anything_is_joined_to_the_next()
    {
        Assert.Equal("Bldg 4. Install a wet sprinkler system in the new warehouse",
            Title("Bldg 4. Install a wet sprinkler system in the new warehouse. Per plans."));
    }

    [Fact]
    public void A_long_title_is_cut_at_a_word_and_says_so()
    {
        var title = Title("Tenant infill of existing suite to include new walls, lighting, plumbing, finishes and adjustments to sprinkler heads throughout");

        Assert.Equal("Tenant infill of existing suite to include new walls, lighting, plumbing…", title);
        Assert.True(title.Length <= LeadTitle.MaxLength);
    }

    [Theory]
    [InlineData("MODIFY EXISTING WET TYPE SPRINKLER SYSTEM FOR NEW CEILING LAYOUT",
        "Modify existing wet type sprinkler system for new ceiling layout")]
    [InlineData("INSTALL NEW NFPA 13R SYSTEM ON THE 4TH FLOOR", "Install new NFPA 13R system on the 4th floor")]
    [InlineData("NEW FACP AND HVAC SHUTDOWN FOR SUITE B2", "New FACP and HVAC shutdown for suite B2")]
    public void Text_written_all_in_capitals_is_set_in_sentence_case(string description, string expected)
    {
        Assert.Equal(expected, Title(description));
    }

    [Fact]
    public void Text_written_mostly_in_capitals_is_set_in_sentence_case_too()
    {
        Assert.Equal("In conjunction with project at 156 example ave, modify existing fire alarms",
            Title("IN CONJUNCTION WITH PROJECT AT 156 EXAMPLE AVE, Modify existing fire alarms"));
    }

    // What counts is the part that is shown, not the part that was cut off.
    [Fact]
    public void A_title_whose_shown_part_is_in_capitals_is_set_in_sentence_case()
    {
        var title = Title("INSTALL NEW SPRINKLER SYSTEM IN THE WAREHOUSE AT 100 EXAMPLE ROAD SUITE 4 and then "
            + "connect it to the existing riser in the pump room on the ground floor of the building");

        Assert.StartsWith("Install new sprinkler system in the warehouse at 100 example road", title);
        Assert.EndsWith("…", title);
    }

    [Fact]
    public void Text_that_already_has_small_letters_keeps_its_own_capitals()
    {
        Assert.Equal("Titan Alarm to install a fire alarm system at Mesa High",
            Title("Titan Alarm to install a fire alarm system at Mesa High"));
    }

    [Theory]
    [InlineData("[ePlan] New fire alarm system for a school.", "New fire alarm system for a school")]
    [InlineData("  new   fire alarm\tsystem \n for a school ", "New fire alarm system for a school")]
    [InlineData("*** INTERIOR DEMOLITION OF NON-BEARING WALLS", "Interior demolition of non-bearing walls")]
    [InlineData("- new fire alarm system for a school", "New fire alarm system for a school")]
    [InlineData("Interior alteration for &quot;&quot;Example Credit Union&quot;&quot; &amp; annex",
        "Interior alteration for \"Example Credit Union\" & annex")]
    public void Tags_stray_spaces_and_web_codes_are_tidied_away(string description, string expected)
    {
        Assert.Equal(expected, Title(description));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("| |")]
    [InlineData("---")]
    public void Without_a_description_the_title_is_the_permit_type_in_plain_words(string? description)
    {
        Assert.Equal("Fire Pump", Title(description, "fire_pump"));
    }

    [Fact]
    public void With_neither_the_title_is_the_category()
    {
        Assert.Equal("Fire Suppression", Title(null, null, FireCategory.FireSuppression));
        Assert.Equal("Fire Suppression", Title(" ", "unknown", FireCategory.FireSuppression));
    }

    [Fact]
    public void The_feed_and_the_lead_page_use_it()
    {
        var row = new LeadRow(Guid.NewGuid(), 90, FireCategory.FireSprinkler, "Reason", DateTime.UtcNow,
            "fire_sprinkler", PermitStatusKind.Active, "1 Main St", "Mesa", "AZ", null, null,
            "RELOCATE 12 SPRINKLER HEADS FOR NEW OFFICE LAYOUT. ALL WORK PER NFPA 13. | Commercial");

        var summary = LeadQueries.ToSummary(row, DateTime.UtcNow);

        Assert.Equal("Relocate 12 sprinkler heads for new office layout", summary.Title);
        Assert.Equal("fire_sprinkler", summary.PermitType);
    }
}
