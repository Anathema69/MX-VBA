import { X } from 'lucide-react';
import type { Category, Product } from '../App';

type ProductDialogProps = {
  category: Category | null;
  product: Product | null;
  onClose: () => void;
};

export default function ProductDialog({ category, product, onClose }: ProductDialogProps) {
  const isEdit = !!product;

  return (
    <div 
      className="fixed inset-0 flex items-center justify-center z-50"
      style={{ backgroundColor: 'rgba(15, 23, 42, 0.5)' }}
    >
      <div 
        className="bg-white relative"
        style={{
          width: '560px',
          height: '600px',
          borderRadius: '16px',
          boxShadow: '0 8px 32px rgba(30, 41, 59, 0.15)',
          display: 'flex',
          flexDirection: 'column'
        }}
      >
        {/* Header */}
        <div 
          className="flex-none"
          style={{ 
            padding: '24px 24px 16px',
            borderBottom: '1px solid #F1F5F9'
          }}
        >
          <div className="flex items-start justify-between mb-2">
            <div>
              <div style={{ fontSize: '20px', fontWeight: '600', color: '#0F172A', marginBottom: '4px', fontFamily: 'Segoe UI, sans-serif' }}>
                {isEdit ? 'Editar Producto' : 'Nuevo Producto'}
              </div>
              <div style={{ fontSize: '13px', color: '#64748B', fontFamily: 'Segoe UI, sans-serif' }}>
                Categoría: {category?.name || 'N/A'}
              </div>
            </div>

            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
              style={{ width: '32px', height: '32px' }}
            >
              <X size={16} style={{ color: '#64748B' }} />
            </button>
          </div>
        </div>

        {/* Form */}
        <div 
          className="flex-1 overflow-y-auto"
          style={{ padding: '24px' }}
        >
          <div className="space-y-4">
            {/* Código y Nombre */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label 
                  className="block mb-2"
                  style={{ 
                    fontSize: '12px', 
                    fontWeight: '600',
                    color: '#64748B',
                    textTransform: 'uppercase',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                >
                  Código <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <input
                  type="text"
                  placeholder="TOR-009"
                  defaultValue={product?.code || ''}
                  className="w-full focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
                  style={{
                    height: '40px',
                    border: '1px solid #E2E8F0',
                    borderRadius: '8px',
                    padding: '0 12px',
                    fontSize: '13px',
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                />
              </div>

              <div>
                <label 
                  className="block mb-2"
                  style={{ 
                    fontSize: '12px', 
                    fontWeight: '600',
                    color: '#64748B',
                    textTransform: 'uppercase',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                >
                  Nombre <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <input
                  type="text"
                  placeholder="Nombre del producto"
                  defaultValue={product?.name || ''}
                  className="w-full focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
                  style={{
                    height: '40px',
                    border: '1px solid #E2E8F0',
                    borderRadius: '8px',
                    padding: '0 12px',
                    fontSize: '13px',
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                />
              </div>
            </div>

            {/* Descripción */}
            <div>
              <label 
                className="block mb-2"
                style={{ 
                  fontSize: '12px', 
                  fontWeight: '600',
                  color: '#64748B',
                  textTransform: 'uppercase',
                  fontFamily: 'Segoe UI, sans-serif'
                }}
              >
                Descripción
              </label>
              <textarea
                placeholder="Descripción detallada del producto..."
                rows={2}
                className="w-full focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all resize-none"
                style={{
                  border: '1px solid #E2E8F0',
                  borderRadius: '8px',
                  padding: '10px 12px',
                  fontSize: '13px',
                  color: '#0F172A',
                  fontFamily: 'Segoe UI, sans-serif'
                }}
              />
            </div>

            {/* Stock actual, Stock mínimo, Unidad */}
            <div className="grid grid-cols-3 gap-4">
              <div>
                <label 
                  className="block mb-2"
                  style={{ 
                    fontSize: '12px', 
                    fontWeight: '600',
                    color: '#64748B',
                    textTransform: 'uppercase',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                >
                  Stock actual
                </label>
                <input
                  type="number"
                  placeholder="0"
                  defaultValue={product?.stock || ''}
                  className="w-full focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
                  style={{
                    height: '40px',
                    border: '1px solid #E2E8F0',
                    borderRadius: '8px',
                    padding: '0 12px',
                    fontSize: '13px',
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                />
              </div>

              <div>
                <label 
                  className="block mb-2"
                  style={{ 
                    fontSize: '12px', 
                    fontWeight: '600',
                    color: '#64748B',
                    textTransform: 'uppercase',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                >
                  Stock mínimo
                </label>
                <input
                  type="number"
                  placeholder="0"
                  defaultValue={product?.minimum || ''}
                  className="w-full focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
                  style={{
                    height: '40px',
                    border: '1px solid #E2E8F0',
                    borderRadius: '8px',
                    padding: '0 12px',
                    fontSize: '13px',
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                />
              </div>

              <div>
                <label 
                  className="block mb-2"
                  style={{ 
                    fontSize: '12px', 
                    fontWeight: '600',
                    color: '#64748B',
                    textTransform: 'uppercase',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                >
                  Unidad
                </label>
                <select
                  defaultValue={product?.unit || 'pza'}
                  className="w-full focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
                  style={{
                    height: '40px',
                    border: '1px solid #E2E8F0',
                    borderRadius: '8px',
                    padding: '0 12px',
                    fontSize: '13px',
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif',
                    backgroundColor: 'white'
                  }}
                >
                  <option value="pza">pza</option>
                  <option value="kg">kg</option>
                  <option value="m">m</option>
                  <option value="l">l</option>
                  <option value="rollo">rollo</option>
                  <option value="caja">caja</option>
                </select>
              </div>
            </div>

            {/* Precio unitario y Ubicación */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label 
                  className="block mb-2"
                  style={{ 
                    fontSize: '12px', 
                    fontWeight: '600',
                    color: '#64748B',
                    textTransform: 'uppercase',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                >
                  Precio unitario
                </label>
                <div className="relative">
                  <span 
                    className="absolute left-3 top-1/2 transform -translate-y-1/2"
                    style={{ fontSize: '13px', color: '#64748B', fontFamily: 'Segoe UI, sans-serif' }}
                  >
                    $
                  </span>
                  <input
                    type="number"
                    step="0.01"
                    placeholder="0.00"
                    defaultValue={product?.price || ''}
                    className="w-full pl-7 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
                    style={{
                      height: '40px',
                      border: '1px solid #E2E8F0',
                      borderRadius: '8px',
                      padding: '0 12px',
                      fontSize: '13px',
                      color: '#0F172A',
                      fontFamily: 'Segoe UI, sans-serif'
                    }}
                  />
                </div>
              </div>

              <div>
                <label 
                  className="block mb-2"
                  style={{ 
                    fontSize: '12px', 
                    fontWeight: '600',
                    color: '#64748B',
                    textTransform: 'uppercase',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                >
                  Ubicación
                </label>
                <input
                  type="text"
                  placeholder="Ej: A-1, Estante 3"
                  defaultValue={product?.location || ''}
                  className="w-full focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
                  style={{
                    height: '40px',
                    border: '1px solid #E2E8F0',
                    borderRadius: '8px',
                    padding: '0 12px',
                    fontSize: '13px',
                    color: '#0F172A',
                    fontFamily: 'Segoe UI, sans-serif'
                  }}
                />
              </div>
            </div>

            {/* Proveedor */}
            <div>
              <label 
                className="block mb-2"
                style={{ 
                  fontSize: '12px', 
                  fontWeight: '600',
                  color: '#64748B',
                  textTransform: 'uppercase',
                  fontFamily: 'Segoe UI, sans-serif'
                }}
              >
                Proveedor
              </label>
              <select
                className="w-full focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
                style={{
                  height: '40px',
                  border: '1px solid #E2E8F0',
                  borderRadius: '8px',
                  padding: '0 12px',
                  fontSize: '13px',
                  color: '#0F172A',
                  fontFamily: 'Segoe UI, sans-serif',
                  backgroundColor: 'white'
                }}
              >
                <option value="">Seleccionar proveedor...</option>
                <option value="1">Distribuidora Eléctrica MX</option>
                <option value="2">Tornillos Industriales SA</option>
                <option value="3">Cables y Más</option>
              </select>
            </div>

            {/* Notas */}
            <div>
              <label 
                className="block mb-2"
                style={{ 
                  fontSize: '12px', 
                  fontWeight: '600',
                  color: '#64748B',
                  textTransform: 'uppercase',
                  fontFamily: 'Segoe UI, sans-serif'
                }}
              >
                Notas
              </label>
              <textarea
                placeholder="Notas adicionales (opcional)..."
                rows={3}
                className="w-full focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all resize-none"
                style={{
                  border: '1px solid #E2E8F0',
                  borderRadius: '8px',
                  padding: '10px 12px',
                  fontSize: '13px',
                  color: '#0F172A',
                  fontFamily: 'Segoe UI, sans-serif'
                }}
              />
            </div>
          </div>
        </div>

        {/* Footer */}
        <div 
          className="flex-none flex items-center justify-between"
          style={{ 
            borderTop: '1px solid #F1F5F9',
            padding: '20px 24px'
          }}
        >
          <button
            onClick={onClose}
            className="px-4 hover:bg-gray-50 transition-colors"
            style={{
              height: '40px',
              border: '1px solid #E2E8F0',
              borderRadius: '8px',
              fontSize: '13px',
              color: '#0F172A',
              fontFamily: 'Segoe UI, sans-serif',
              backgroundColor: 'white'
            }}
          >
            Cancelar
          </button>

          <button
            className="px-6 hover:opacity-90 transition-opacity"
            style={{
              height: '40px',
              backgroundColor: '#1D4ED8',
              color: 'white',
              borderRadius: '8px',
              fontSize: '13px',
              fontWeight: '600',
              fontFamily: 'Segoe UI, sans-serif',
              border: 'none'
            }}
          >
            Guardar Producto
          </button>
        </div>
      </div>
    </div>
  );
}
