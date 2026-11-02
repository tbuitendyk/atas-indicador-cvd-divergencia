// Pruebas de SuiteOrderFlowStacker sin ATAS ni Windows. Devuelve el número de fallos (0 = todo bien).
var c = new Comprobador();

PruebasNucleo.Ejecutar(c);

return c.Resumen();
