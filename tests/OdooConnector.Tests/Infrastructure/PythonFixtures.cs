namespace OdooConnector.Tests.Infrastructure;

/// <summary>
/// XML-RPC documents written by Python's <c>xmlrpc.client</c> — the marshaller Odoo uses for its responses —
/// so the parser is tested against real output rather than hand-written XML.
/// </summary>
internal static class PythonFixtures
{
    /// <summary><c>search_read</c> on <c>res.partner</c>: many2one pairs, <c>False</c> for empty fields, escaped text.</summary>
    public const string PartnersSearchRead = """
        <?xml version='1.0'?>
        <methodResponse>
        <params>
        <param>
        <value><array><data>
        <value><struct>
        <member>
        <name>id</name>
        <value><int>14</int></value>
        </member>
        <member>
        <name>name</name>
        <value><string>Azure Interior</string></value>
        </member>
        <member>
        <name>is_company</name>
        <value><boolean>1</boolean></value>
        </member>
        <member>
        <name>email</name>
        <value><string>azure.interior24@example.com</string></value>
        </member>
        <member>
        <name>phone</name>
        <value><string>(870)-931-0505</string></value>
        </member>
        <member>
        <name>vat</name>
        <value><boolean>0</boolean></value>
        </member>
        <member>
        <name>street</name>
        <value><string>4557 De Silva St</string></value>
        </member>
        <member>
        <name>city</name>
        <value><string>Fremont</string></value>
        </member>
        <member>
        <name>zip</name>
        <value><string>94538</string></value>
        </member>
        <member>
        <name>country_id</name>
        <value><array><data>
        <value><int>233</int></value>
        <value><string>United States</string></value>
        </data></array></value>
        </member>
        <member>
        <name>parent_id</name>
        <value><boolean>0</boolean></value>
        </member>
        </struct></value>
        <value><struct>
        <member>
        <name>id</name>
        <value><int>27</int></value>
        </member>
        <member>
        <name>name</name>
        <value><string>Ñandú &amp; Cía &lt;S.A.S&gt;</string></value>
        </member>
        <member>
        <name>is_company</name>
        <value><boolean>1</boolean></value>
        </member>
        <member>
        <name>email</name>
        <value><boolean>0</boolean></value>
        </member>
        <member>
        <name>phone</name>
        <value><boolean>0</boolean></value>
        </member>
        <member>
        <name>vat</name>
        <value><string>900123456-7</string></value>
        </member>
        <member>
        <name>street</name>
        <value><boolean>0</boolean></value>
        </member>
        <member>
        <name>city</name>
        <value><string>Medellín</string></value>
        </member>
        <member>
        <name>zip</name>
        <value><boolean>0</boolean></value>
        </member>
        <member>
        <name>country_id</name>
        <value><array><data>
        <value><int>49</int></value>
        <value><string>Colombia</string></value>
        </data></array></value>
        </member>
        <member>
        <name>parent_id</name>
        <value><boolean>0</boolean></value>
        </member>
        </struct></value>
        </data></array></value>
        </param>
        </params>
        </methodResponse>
        """;

    /// <summary>A <c>UserError</c> (fault code 2).</summary>
    public const string UserErrorFault = """
        <?xml version='1.0'?>
        <methodResponse>
        <fault>
        <value><struct>
        <member>
        <name>faultCode</name>
        <value><int>2</int></value>
        </member>
        <member>
        <name>faultString</name>
        <value><string>The field 'Customer' is required to post the invoice.</string></value>
        </member>
        </struct></value>
        </fault>
        </methodResponse>
        """;

    /// <summary>An unexpected server error (fault code 1) carrying a Python traceback.</summary>
    public const string ServerErrorFault = """
        <?xml version='1.0'?>
        <methodResponse>
        <fault>
        <value><struct>
        <member>
        <name>faultCode</name>
        <value><int>1</int></value>
        </member>
        <member>
        <name>faultString</name>
        <value><string>Traceback (most recent call last):
          File "/usr/lib/python3/dist-packages/odoo/http.py", line 1766, in _serve_db
            return service_model.retrying(self._serve_ir_http, self.env)
        ValueError: Invalid field 'mobile_phone' on model 'res.partner'
        </string></value>
        </member>
        </struct></value>
        </fault>
        </methodResponse>
        """;

    /// <summary>
    /// <c>[42, -7, True, False, 1190.0, 99.99, '  padded text  ', '', None, Binary, DateTime, {}, []]</c>,
    /// dumped with <c>allow_none=True</c>.
    /// </summary>
    public const string Scalars = """
        <?xml version='1.0'?>
        <methodResponse>
        <params>
        <param>
        <value><array><data>
        <value><int>42</int></value>
        <value><int>-7</int></value>
        <value><boolean>1</boolean></value>
        <value><boolean>0</boolean></value>
        <value><double>1190.0</double></value>
        <value><double>99.99</double></value>
        <value><string>  padded text  </string></value>
        <value><string></string></value>
        <value><nil/></value><value><base64>
        JVBERi0xLjcgZGVtbw==
        </base64></value>
        <value><dateTime.iso8601>20261004T13:45:00</dateTime.iso8601></value>
        <value><struct>
        </struct></value>
        <value><array><data>
        </data></array></value>
        </data></array></value>
        </param>
        </params>
        </methodResponse>
        """;
}
