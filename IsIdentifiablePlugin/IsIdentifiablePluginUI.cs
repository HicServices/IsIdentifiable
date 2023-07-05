using Rdmp.Core;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.Core.Curation.Data;
using System.Collections.Generic;

namespace IsIdentifiablePlugin;

class IsIdentifiablePluginUI : PluginUserInterface
{
    public IsIdentifiablePluginUI(IBasicActivateItems itemActivator)
        : base(itemActivator)
    { }

    public override IEnumerable<IAtomicCommand> GetAdditionalRightClickMenuItems(object o)
    {
        if (o is Catalogue c)
        {
            yield return new ExecuteCommandRunIsIdentifiable(BasicActivator, c, null) { SuggestedCategory = "IsIdentifiable" };
        }
    }
}
