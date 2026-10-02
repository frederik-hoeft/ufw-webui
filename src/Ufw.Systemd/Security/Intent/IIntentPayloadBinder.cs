using System.Text.Json;
﻿using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal interface IIntentPayloadBinder<TPayload> where TPayload : class
{
    IntentPayloadBindingResult<TPayload> Bind(ISignedIntent intent);
}
