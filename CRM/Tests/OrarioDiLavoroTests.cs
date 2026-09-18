using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// Cosa vuol dire "orario di lavoro configurato".
/// <para>
/// Il difetto presidiato, letto sui dati veri: in archivio l'orario non impostato era
/// 00:00-00:00 (residuo di una migration), e tre lettori lo interpretavano in tre modi. Il
/// piu' grave: il salvataggio di un ticket con orario veniva respinto come "fuori
/// dall'intervallo 00:00-00:00", e infatti nessun ticket in archivio aveva un orario.
/// </para>
/// </summary>
public class OrarioDiLavoroTests
{
    [Fact]
    public void Vuoto_se_manca_un_estremo()
    {
        Assert.Null(new GlobalSetting { ScheduleTimeStart = new TimeOnly(8, 0) }.OrarioDiLavoro());
        Assert.Null(new GlobalSetting { ScheduleTimeEnd = new TimeOnly(18, 0) }.OrarioDiLavoro());
        Assert.Null(new GlobalSetting().OrarioDiLavoro());
    }

    [Fact]
    public void Mezzanotte_mezzanotte_vale_come_non_configurato()
    {
        var settings = new GlobalSetting { ScheduleTimeStart = TimeOnly.MinValue, ScheduleTimeEnd = TimeOnly.MinValue };

        Assert.Null(settings.OrarioDiLavoro());
    }

    [Fact]
    public void Una_mezzanotte_sola_basta_a_invalidarlo()
    {
        Assert.Null(new GlobalSetting { ScheduleTimeStart = TimeOnly.MinValue, ScheduleTimeEnd = new TimeOnly(18, 0) }.OrarioDiLavoro());
        Assert.Null(new GlobalSetting { ScheduleTimeStart = new TimeOnly(8, 0), ScheduleTimeEnd = TimeOnly.MinValue }.OrarioDiLavoro());
    }

    [Fact]
    public void Un_inizio_che_non_precede_la_fine_non_e_un_orario()
    {
        Assert.Null(new GlobalSetting { ScheduleTimeStart = new TimeOnly(18, 0), ScheduleTimeEnd = new TimeOnly(8, 0) }.OrarioDiLavoro());
        Assert.Null(new GlobalSetting { ScheduleTimeStart = new TimeOnly(9, 0), ScheduleTimeEnd = new TimeOnly(9, 0) }.OrarioDiLavoro());
    }

    [Fact]
    public void L_orario_configurato_torna_intero()
    {
        var orario = new GlobalSetting { ScheduleTimeStart = new TimeOnly(8, 30), ScheduleTimeEnd = new TimeOnly(17, 30) }.OrarioDiLavoro();

        Assert.NotNull(orario);
        Assert.Equal(new TimeOnly(8, 30), orario.Inizio);
        Assert.Equal(new TimeOnly(17, 30), orario.Fine);
    }
}
