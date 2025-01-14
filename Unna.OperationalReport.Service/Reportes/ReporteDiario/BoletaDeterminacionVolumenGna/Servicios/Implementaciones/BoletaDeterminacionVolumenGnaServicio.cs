﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unna.OperationalReport.Data.Configuracion.Repositorios.Abstracciones;
using Unna.OperationalReport.Data.Registro.Entidades;
using Unna.OperationalReport.Data.Registro.Enums;
using Unna.OperationalReport.Data.Registro.Repositorios.Abstracciones;
using Unna.OperationalReport.Data.Reporte.Enums;
using Unna.OperationalReport.Data.Reporte.Repositorios.Abstracciones;
using Unna.OperationalReport.Data.Reporte.Repositorios.Implementaciones;
using Unna.OperationalReport.Service.Registros.DiaOperativos.Dtos;
using Unna.OperationalReport.Service.Reportes.Generales.Servicios.Abstracciones;
using Unna.OperationalReport.Service.Reportes.Impresiones.Dtos;
using Unna.OperationalReport.Service.Reportes.Impresiones.Servicios.Abstracciones;
using Unna.OperationalReport.Service.Reportes.ReporteDiario.BoletaDeterminacionVolumenGna.Dtos;
using Unna.OperationalReport.Service.Reportes.ReporteDiario.BoletaDeterminacionVolumenGna.Servicios.Abstracciones;
using Unna.OperationalReport.Service.Reportes.ReporteDiario.FiscalizacionPetroPeru.Dtos;
using Unna.OperationalReport.Service.Reportes.ReporteDiario.FiscalizacionProductos.Servicios.Abstracciones;
using Unna.OperationalReport.Service.Usuarios.Servicios.Abstracciones;
using Unna.OperationalReport.Tools.Comunes.Infraestructura.Dtos;
using Unna.OperationalReport.Tools.Comunes.Infraestructura.Utilitarios;

namespace Unna.OperationalReport.Service.Reportes.ReporteDiario.BoletaDeterminacionVolumenGna.Servicios.Implementaciones
{
    public class BoletaDeterminacionVolumenGnaServicio : IBoletaDeterminacionVolumenGnaServicio
    {

        private readonly IRegistroRepositorio _registroRepositorio;
        private readonly IReporteServicio _reporteServicio;
        private readonly IImpresionServicio _impresionServicio;
        private readonly IBoletaDiariaFiscalizacionRepositorio _boletaDiariaFiscalizacionRepositorio;
        private readonly IGnsVolumeMsYPcBrutoRepositorio _gnsVolumeMsYPcBrutoRepositorio;
        private readonly IFiscalizacionProductosServicio _fiscalizacionProductosServicio;
        private readonly IReporteDiariaDatosRepositorio _reporteDiariaDatosRepositorio;

        public BoletaDeterminacionVolumenGnaServicio(
            IRegistroRepositorio registroRepositorio,
            IReporteServicio reporteServicio,
            IImpresionServicio impresionServicio,
            IBoletaDiariaFiscalizacionRepositorio boletaDiariaFiscalizacionRepositorio,
            IGnsVolumeMsYPcBrutoRepositorio gnsVolumeMsYPcBrutoRepositorio,
            IFiscalizacionProductosServicio fiscalizacionProductosServicio,
            IReporteDiariaDatosRepositorio reporteDiariaDatosRepositorio
            )
        {
            _registroRepositorio = registroRepositorio;
            _reporteServicio = reporteServicio;
            _impresionServicio = impresionServicio;
            _boletaDiariaFiscalizacionRepositorio = boletaDiariaFiscalizacionRepositorio;
            _gnsVolumeMsYPcBrutoRepositorio = gnsVolumeMsYPcBrutoRepositorio;
            _fiscalizacionProductosServicio = fiscalizacionProductosServicio;
            _reporteDiariaDatosRepositorio = reporteDiariaDatosRepositorio;
        }
        public async Task<OperacionDto<BoletaDeterminacionVolumenGnaDto>> ObtenerAsync(long idUsuario)
        {
            var operacionGeneral = await _reporteServicio.ObtenerAsync((int)TiposReportes.BoletaDeterminacionVolumenGnaFiscalizado, idUsuario);
            if (!operacionGeneral.Completado)
            {
                return new OperacionDto<BoletaDeterminacionVolumenGnaDto>(CodigosOperacionDto.NoExiste, operacionGeneral.Mensajes);
            }

            var operacionImpresion = await _impresionServicio.ObtenerAsync((int)TiposReportes.BoletaDeterminacionVolumenGnaFiscalizado, FechasUtilitario.ObtenerDiaOperativo());
            if (operacionImpresion.Completado && operacionImpresion.Resultado != null && !string.IsNullOrWhiteSpace(operacionImpresion.Resultado.Datos))
            {
                var rpta = JsonConvert.DeserializeObject<BoletaDeterminacionVolumenGnaDto>(operacionImpresion.Resultado.Datos);
                rpta.General = operacionGeneral.Resultado;
                return new OperacionDto<BoletaDeterminacionVolumenGnaDto>(rpta);
            }

            DateTime diaOperativo = FechasUtilitario.ObtenerDiaOperativo();
            var dto = new BoletaDeterminacionVolumenGnaDto
            {
                Fecha = diaOperativo.ToString("dd/MM/yyyy"),
                General = operacionGeneral.Resultado

            };

            // Cuadro N° 1. Asignación de Volumen de Gas Combustible (GC) - LOTE IV
            var volumenTotalGasCombustible = await _gnsVolumeMsYPcBrutoRepositorio.ObtenerPorTipoYNombreDiaOperativoAsync(TiposTablasSupervisorPgt.VolumenMsGnsAgpsa, TiposGnsVolumeMsYPcBruto.GnsParaConsumoPropio, diaOperativo);
            if (volumenTotalGasCombustible != null)
            {
                dto.VolumenTotalGasCombustible = volumenTotalGasCombustible.VolumeMs;
            }
            dto.FactoresAsignacionGasCombustible = await ObtenerFactoresAsignacionGasCombustibleAsync(diaOperativo, dto.VolumenTotalGasCombustible ?? 0);

            var volumenTotalGns = await _gnsVolumeMsYPcBrutoRepositorio.ObtenerPorTipoYNombreDiaOperativoAsync(TiposTablasSupervisorPgt.VolumenMsGnsAgpsa, TiposGnsVolumeMsYPcBruto.GnsAEgpsa, diaOperativo);
            if (volumenTotalGns != null)
            {
                dto.VolumenTotalGns = volumenTotalGns.VolumeMs;
            }
            dto.FactorAsignacionGns = await ObtenerFactoresAsignacionGasCombustibleAsync(diaOperativo, dto.VolumenTotalGns ?? 0);




            // Cuadro N° 3. Asignación de Volumen de Líquidos del Gas Natural (LGN) - LOTE IV
            var operacionFiscalizacionProductos = await _fiscalizacionProductosServicio.ObtenerAsync(idUsuario);
            if (operacionFiscalizacionProductos.Completado && operacionFiscalizacionProductos.Resultado != null && operacionFiscalizacionProductos.Resultado.ProductoGlpCgn != null)
            {
                var entidadGlp = operacionFiscalizacionProductos.Resultado.ProductoGlpCgn.Where(e => e.Producto.Equals("GLP")).FirstOrDefault();
                dto.VolumenProduccionTotalGlp = entidadGlp != null ? entidadGlp.Produccion : 0;

                var entidadCgn = operacionFiscalizacionProductos.Resultado.ProductoGlpCgn.Where(e => e.Producto.Equals("CGN")).FirstOrDefault();
                dto.VolumenProduccionTotalCgn = entidadCgn != null ? entidadCgn.Produccion : 0;
                dto.VolumenProduccionTotalLgn = dto.VolumenProduccionTotalGlp + dto.VolumenProduccionTotalCgn;
            }
            dto.FactorAsignacionLiquidosGasNatural = await ObtenerFactorAsignacionLiquidosGasNaturalAsync(diaOperativo, dto.VolumenProduccionTotalLgn ?? 0);
            
            
            if (dto.VolumenProduccionTotalLgn > 0)
            {
                var asignacionLgn = dto.FactorAsignacionLiquidosGasNatural.Where(e => e.Item == 5).FirstOrDefault();
                if (asignacionLgn != null && asignacionLgn.Asignacion != null)
                {
                    dto.VolumenProduccionTotalGlpLoteIv = Math.Round(asignacionLgn.Asignacion??0 * dto.VolumenProduccionTotalGlp??0 / dto.VolumenProduccionTotalLgn.Value,2);
                    dto.VolumenProduccionTotalCgnLoteIv = Math.Round(asignacionLgn.Asignacion ?? 0 - dto.VolumenProduccionTotalGlpLoteIv ?? 0, 2);
                }                
            }
            double eficienciaCenjate = 0;
            var totalEnergiaMmbtu = dto.FactorAsignacionLiquidosGasNatural.Where(e=>e.Suministrador.Equals("Total")).FirstOrDefault();
            if (totalEnergiaMmbtu != null)
            {
                if (totalEnergiaMmbtu.Contenido > 0)
                {
                    eficienciaCenjate = ((dto.VolumenProduccionTotalLgn ??0 / totalEnergiaMmbtu.Contenido??0) / 42) * 100;
                }
                
            }
            
            var factorConversion = await _reporteDiariaDatosRepositorio.ObtenerFactorConversionPorLotePetroperuAsync(diaOperativo,(int)TiposLote.LoteIv,(int)TiposDatos.VolumenMpcd, eficienciaCenjate);
            if (factorConversion != null)
            {
                dto.FactorCoversion = factorConversion.Value;
            }
            
            // Cuadro N° 4. Volumen Fiscalizado del Gas Natural Asociado (GNA) - LOTE IV

            DistribucionGasNaturalAsociadoDto distribucionGasNaturalAsociado = new DistribucionGasNaturalAsociadoDto
            {
                Suministrador = "UNNA ENERGIA (LOTE IV)",
                GasCombustible = dto.FactoresAsignacionGasCombustible.Where(e => e.Item == 5).FirstOrDefault() != null ? dto.FactoresAsignacionGasCombustible.Where(e => e.Item == 5)?.FirstOrDefault()?.FactorAsignacion : 0,
            };
            var unnaLoteIv = dto.FactorAsignacionLiquidosGasNatural.Where(e => e.Item == 5).FirstOrDefault();
            if (unnaLoteIv != null)
            {
                distribucionGasNaturalAsociado.VolumenGns = Math.Round(((unnaLoteIv.Asignacion ?? 0) * TiposValoresFijos.Conversion42 * dto.FactorCoversion ?? 0 / 1000), 4);
                distribucionGasNaturalAsociado.VolumenGna = Math.Round(unnaLoteIv.Volumen ?? 0, 4);
            }
            distribucionGasNaturalAsociado.VolumenGnsd = distribucionGasNaturalAsociado.VolumenGna - distribucionGasNaturalAsociado.VolumenGns - distribucionGasNaturalAsociado.VolumenGnsd;
            dto.DistribucionGasNaturalAsociado = distribucionGasNaturalAsociado;

            // Totales
                        
            var volumenGnsVentaVgnsvEnel = await _registroRepositorio.ObtenerValorAsync((int)TiposDatos.GnsVentaUnnaLoteIv, (int)TiposLote.LoteX, diaOperativo,(int)TiposNumeroRegistro.SegundoRegistro);
            if (volumenGnsVentaVgnsvEnel != null)
            {
                dto.VolumenGnsVentaVgnsvEnel = volumenGnsVentaVgnsvEnel.Valor;
            }

            var volumenVolLimaGas = await _gnsVolumeMsYPcBrutoRepositorio.ObtenerPorTipoYNombreDiaOperativoAsync(TiposTablasSupervisorPgt.VolumenVolLimaGas, TiposGnsVolumeMsYPcBruto.VolLimaGas, diaOperativo);
            if (volumenVolLimaGas != null)
            {
                dto.VolumenGnsVentaVgnsvLimagas = volumenVolLimaGas.VolumeMs;
            }

            var volGasnorp = await _gnsVolumeMsYPcBrutoRepositorio.ObtenerPorTipoYNombreDiaOperativoAsync(TiposTablasSupervisorPgt.VolumenVolLimaGas, TiposGnsVolumeMsYPcBruto.VolGasnorp, diaOperativo);
            if (volGasnorp != null)
            {
                dto.VolumenGnsVentaVgnsvGasnorp = volGasnorp.VolumeMs;
            }

            dto.VolumenGnsVentaVgnsvTotal = dto.VolumenGnsVentaVgnsvEnel + dto.VolumenGnsVentaVgnsvGasnorp + dto.VolumenGnsVentaVgnsvLimagas;
            dto.VolumenGnsFlareVgnsrf = Math.Round(distribucionGasNaturalAsociado.VolumenGnsd??0 - dto.VolumenGnsVentaVgnsvTotal??0,4);


            dto.SumaVolumenGasCombustibleVolumen = dto.DistribucionGasNaturalAsociado.GasCombustible + dto.DistribucionGasNaturalAsociado.VolumenGns;
            dto.VolumenGnaFiscalizado = dto.DistribucionGasNaturalAsociado.VolumenGna - dto.VolumenGnsFlareVgnsrf;

            return new OperacionDto<BoletaDeterminacionVolumenGnaDto>(dto);
        }

        private async Task<List<FactoresAsignacionGasCombustibleDto>> ObtenerFactoresAsignacionGasCombustibleAsync(DateTime diaOperativo, double volumenTotalGasCombustible)
        {
            var entidades = await _boletaDiariaFiscalizacionRepositorio.ListarRegistroPorDiaOperativoFactorAsignacionAsync(diaOperativo, (int)TiposDatos.VolumenMpcd, (int)TiposDatos.Riqueza, (int)TiposDatos.PoderCalorifico);
            var lista = entidades.Select(e => new FactoresAsignacionGasCombustibleDto()
            {
                Item = e.Item,
                Suministrador = e.Suministrador,
                Volumen = e.Volumen,
                Calorifico = e.Calorifico,
                EnergiaMmbtu = (e.Volumen * e.Calorifico) / 1000
            }).ToList();

            double totalEnergiaMmbtu = lista.Sum(e => e.EnergiaMmbtu ?? 0);
            if (totalEnergiaMmbtu > 0)
            {
                lista.ForEach(e => e.FactorAsignacion = (e.EnergiaMmbtu / totalEnergiaMmbtu));
            }
            lista.ForEach(e => e.Asignacion = (e.FactorAsignacion / volumenTotalGasCombustible));

            var total = new FactoresAsignacionGasCombustibleDto
            {
                Suministrador = "Total",
                Volumen = lista.Sum(e => e.Volumen),
                FactorAsignacion = lista.Sum(e => e.FactorAsignacion),
                Asignacion = lista.Sum(e => e.Asignacion)
            };
            total.Calorifico = lista.Sum(e=>e.VolumenCalorifico) / total.Volumen;
            total.EnergiaMmbtu = total.Volumen * total.Calorifico / 1000;
            lista.Add(total);
            return lista;
        }


        private async Task<List<FactorAsignacionLiquidosGasNaturalDto>> ObtenerFactorAsignacionLiquidosGasNaturalAsync(DateTime diaOperativo, double volumenTotalProduccion)
        {
            var entidades = await _boletaDiariaFiscalizacionRepositorio.ListarRegistroPorDiaOperativoFactorAsignacionAsync(diaOperativo, (int)TiposDatos.VolumenMpcd, (int)TiposDatos.Riqueza, (int)TiposDatos.PoderCalorifico);
            var lista = entidades.Select(e => new FactorAsignacionLiquidosGasNaturalDto()
            {
                Item = e.Item,
                Suministrador = e.Suministrador,
                Volumen = e.Volumen,
                Riqueza = e.Riqueza,
                Contenido = (e.Volumen * e.Riqueza)
            }).ToList();

            double totalContenido = lista.Sum(e => e.Contenido ?? 0);
            if (totalContenido > 0)
            {
                lista.ForEach(e => e.FactorAsignacion = (e.Contenido / totalContenido) * 100);
            }
            lista.ForEach(e => e.Asignacion = (e.FactorAsignacion / volumenTotalProduccion));


            var total = new FactorAsignacionLiquidosGasNaturalDto
            {
                Suministrador = "Total",
                Volumen = lista.Sum(e => e.Volumen),
                FactorAsignacion = lista.Sum(e => e.FactorAsignacion),
                Asignacion = lista.Sum(e => e.Asignacion),
                Contenido = lista.Sum(e => e.Contenido)
            };
            total.Riqueza = lista.Sum(e => e.VolumenRiqueza) / total.Volumen;
            
            lista.Add(total);

            return lista;
        }






        public async Task<OperacionDto<RespuestaSimpleDto<bool>>> GuardarAsync(BoletaDeterminacionVolumenGnaDto peticion)
        {
            var operacionValidacion = ValidacionUtilitario.ValidarModelo<RespuestaSimpleDto<bool>>(peticion);
            if (!operacionValidacion.Completado)
            {
                return operacionValidacion;
            }
            peticion.General = null;
            var dto = new ImpresionDto()
            {
                IdConfiguracion = RijndaelUtilitario.EncryptRijndaelToUrl((int)TiposReportes.BoletaDeterminacionVolumenGnaFiscalizado),
                Fecha = FechasUtilitario.ObtenerDiaOperativo(),
                IdUsuario = peticion.IdUsuario,
                Datos = JsonConvert.SerializeObject(peticion)
            };
            return await _impresionServicio.GuardarAsync(dto);

        }


        

    }
}
