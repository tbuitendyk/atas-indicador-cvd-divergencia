// Pruebas de SuiteOrderFlowStacker sin ATAS ni Windows. Devuelve el número de fallos (0 = todo bien).
var c = new Comprobador();

PruebasNucleo.Ejecutar(c);
PruebasIndicador.Ejecutar(c);
PruebasBus.Ejecutar(c);
PruebasV2Barras.Ejecutar(c);
PruebasV2Clasificador.Ejecutar(c);
PruebasV2Lvn.Ejecutar(c);
PruebasV2Fvg.Ejecutar(c);
PruebasV2Gatillo.Ejecutar(c);
PruebasV2Modelos.Ejecutar(c);
PruebasV2Panel.Ejecutar(c);
PruebasV2Manual.Ejecutar(c);
Documentacion.Ejecutar(c);

return c.Resumen();
